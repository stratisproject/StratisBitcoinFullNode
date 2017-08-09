using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The BlockStoreLoop simultaneously finds and downloads blocks
    /// and stores them in the BlockRepository
    /// <see cref="DownloadAndStoreBlocks"/>
    /// </summary>
    public sealed class BlockStoreLoop
    {
        internal readonly ConcurrentChain Chain;
        public BlockRepository BlockRepository { get; }
        private readonly NodeSettings nodeArgs;
        internal StoreBlockPuller BlockPuller { get; private set; }
        private readonly BlockStoreCache blockStoreCache;
        private readonly INodeLifetime nodeLifetime;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly BlockStoreStats blockStoreStats;
        private readonly ILogger storeLogger;
        private BlockStoreStepChain stepChain;

        internal ChainState ChainState { get; }
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

        /// <summary>
        /// The highest stored block in the repository
        /// </summary>
        internal ChainedBlock StoreTip { get; private set; }

        internal uint InsertBlockSizeThreshold = 1000000 * 5; // Block.MAX_BLOCK_SIZE // Should be configurable 
        internal int PendingStorageBatchThreshold = 5;  // Should be configurable
        internal int BatchDownloadSize = 1000; // Should be configurable
        private TimeSpan pushInterval = TimeSpan.FromSeconds(10);
        internal readonly TimeSpan PushIntervalIBD = TimeSpan.FromMilliseconds(100);

        /// <summary>
        /// Public constructor for unit testing
        /// </summary>
        public BlockStoreLoop(IAsyncLoopFactory asyncLoopFactory,
            StoreBlockPuller blockPuller,
            BlockRepository blockRepository,
            BlockStoreCache cache,
            ConcurrentChain chain,
            ChainState chainState,
            NodeSettings nodeArgs,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.BlockPuller = blockPuller;
            this.BlockRepository = blockRepository;
            this.Chain = chain;
            this.ChainState = chainState;
            this.blockStoreCache = cache;
            this.nodeLifetime = nodeLifetime;
            this.nodeArgs = nodeArgs;
            this.storeLogger = loggerFactory.CreateLogger(GetType().FullName);

            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, this.blockStoreCache, this.storeLogger);
        }

        /// <summary>
        /// Initialize the BlockStore
        /// <para>
        /// If StoreTip is null, the store is out of sync.
        /// 
        /// This can happen when:
        ///     1: The node crashed
        ///     2: The node was not closed down properly
        ///     
        /// To recover we walk back the chain until a common block header is found 
        /// and set the BlockStore's StoreTip to that
        /// </para>                
        /// </summary>
        public async Task Initialize()
        {
            if (this.nodeArgs.Store.ReIndex)
                throw new NotImplementedException();

            this.StoreTip = this.Chain.GetBlock(this.BlockRepository.BlockHash);

            if (this.StoreTip == null)
            {
                var blockStoreResetList = new List<uint256>();
                Block resetBlock = await this.BlockRepository.GetAsync(this.BlockRepository.BlockHash);
                uint256 resetBlockHash = resetBlock.GetHash();

                while (this.Chain.GetBlock(resetBlockHash) == null)
                {
                    blockStoreResetList.Add(resetBlockHash);

                    if (resetBlock.Header.HashPrevBlock == this.Chain.Genesis.HashBlock)
                    {
                        resetBlockHash = this.Chain.Genesis.HashBlock;
                        break;
                    }

                    resetBlock = await this.BlockRepository.GetAsync(resetBlock.Header.HashPrevBlock);
                    Guard.NotNull(resetBlock, nameof(resetBlock));
                    resetBlockHash = resetBlock.GetHash();
                }

                ChainedBlock newTip = this.Chain.GetBlock(resetBlockHash);
                await this.BlockRepository.DeleteAsync(newTip.HashBlock, blockStoreResetList);
                this.StoreTip = newTip;
                this.storeLogger.LogWarning($"BlockStore Initialize recovering to block height = {newTip.Height} hash = {newTip.HashBlock}");
            }

            if (this.nodeArgs.Store.TxIndex != this.BlockRepository.TxIndex)
            {
                if (this.StoreTip != this.Chain.Genesis)
                    throw new BlockStoreException("You need to rebuild the database using -reindex-chainstate to change -txindex");
                if (this.nodeArgs.Store.TxIndex)
                    await this.BlockRepository.SetTxIndex(this.nodeArgs.Store.TxIndex);
            }

            this.ChainState.HighestPersistedBlock = this.StoreTip;

            this.stepChain = new BlockStoreStepChain();
            this.stepChain.SetNextStep(new ReorganiseBlockRepositoryStep(this));
            this.stepChain.SetNextStep(new CheckNextChainedBlockExistStep(this));
            this.stepChain.SetNextStep(new ProcessPendingStorageStep(this));
            this.stepChain.SetNextStep(new DownloadBlockStep(this));

            StartLoop();
        }

        /// <summary>
        /// Adds a block to Pending Storage
        /// <para>
        /// The BlockStoreSignaler calls AddToPending.
        /// </para>
        /// <para>
        /// Only add the block to pending storage if:
        ///     1: The block does exist on the chain
        ///     2: The store's tip is less than the block to add's height
        /// </para>
        /// </summary>
        /// <param name="block"></param>
        /// <remarks>Possibly check the size of pending in memory</remarks>
        public void AddToPending(Block block)
        {
            ChainedBlock chainedBlock = this.Chain.GetBlock(block.GetHash());
            if (chainedBlock == null)
                return;

            if (this.StoreTip.Height < chainedBlock.Height)
                this.PendingStorage.TryAdd(chainedBlock.HashBlock, new BlockPair(block, chainedBlock));
        }

        /// <summary>
        /// Flush the BlockStore by calling DownloadAndStoreBlocks with disposeMode of true
        /// <para>
        /// This happens when the node shuts down
        /// </para>
        /// </summary>
        public Task Flush()
        {
            return DownloadAndStoreBlocks(CancellationToken.None, true);
        }

        /// <summary>
        /// A loop that:
        ///     1: Writes pending blocks to store 
        ///     2: Download missing blocks and write to store
        /// </summary>
        internal void StartLoop()
        {
            this.asyncLoopFactory.Run("BlockStoreLoop.DownloadBlocks", async token =>
                {
                    await DownloadAndStoreBlocks(this.nodeLifetime.ApplicationStopping);
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpans.Second,
                startAfter: TimeSpans.FiveSeconds);
        }

        /// <summary>
        /// Finds and downloads blocks to store in the Block Repository
        /// <para>
        /// This method executes a chain of steps in order:
        ///     1: Reorganise the repository
        ///     2: Check if the next chained block exists
        ///     3: Process the blocks in pending storage
        ///     4: Find and download blocks
        /// </para>
        /// <para>
        /// All the steps return a BlockStoreLoopStepResult which either signals the While loop
        /// to break or continue execution.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="disposeMode"></param>
        /// <remarks>
        /// TODO: add support to BlockStoreLoop to unset LazyLoadingOn when not in IBD
        /// When in IBD we may need many reads for the block key without fetching the block
        /// So the repo starts with LazyLoadingOn = true, however when not anymore in IBD 
        /// a read is normally done when a peer is asking for the entire block (not just the key) 
        /// then if LazyLoadingOn = false the read will be faster on the entire block      
        /// </remarks>
        private async Task DownloadAndStoreBlocks(CancellationToken cancellationToken, bool disposeMode = false)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.StoreTip.Height >= this.ChainState.HighestValidatedPoW?.Height)
                    break;

                ChainedBlock nextChainedBlock = this.Chain.GetBlock(this.StoreTip.Height + 1);
                if (nextChainedBlock == null)
                    break;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                BlockStoreLoopStepResult result = await this.stepChain.Execute(nextChainedBlock, disposeMode, cancellationToken);
                if (result.ShouldBreak)
                    break;
                if (result.ShouldContinue)
                    continue;
            }
        }

        /// <summary>
        /// Sets the highest stored block
        /// </summary>
        internal void SetStoreTip(ChainedBlock nextChainedBlock)
        {
            Guard.NotNull(nextChainedBlock, "nextChainedBlock");

            this.StoreTip = nextChainedBlock;
            this.ChainState.HighestPersistedBlock = nextChainedBlock;
        }
    }
}