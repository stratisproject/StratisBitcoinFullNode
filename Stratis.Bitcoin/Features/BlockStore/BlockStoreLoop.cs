using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
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
    /// The BlockStoreLoop simultaneously finds and downloads blocks and stores them in the BlockRepository.
    /// </summary>
    public class BlockStoreLoop
    {
        /// <summary> Best chain of block headers.</summary>
        internal readonly ConcurrentChain Chain;

        public StoreBlockPuller BlockPuller { get; }
        public IBlockRepository BlockRepository { get; }
        public virtual string StoreName { get { return GetType().Name; } }

        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly BlockStoreStats blockStoreStats;
        private readonly NodeSettings nodeArgs;
        private readonly INodeLifetime nodeLifetime;
        private readonly ILogger storeLogger;

        /// <summary>The chain of steps that gets executed to find and download blocks.</summary>
        private BlockStoreStepChain stepChain;

        public ChainState ChainState { get; }

        /// <summary>Blocks that in PendingStorage will be processed first before new blocks are downloaded.</summary>
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

        /// <summary>The highest stored block in the repository</summary>
        internal ChainedBlock StoreTip { get; private set; }

        /// <summary>TODO: Should be configurable?</summary>
        internal uint InsertBlockSizeThreshold = 1000000 * 5;

        /// <summary>TODO: Should be configurable?</summary>
        internal int PendingStorageBatchThreshold = 5;

        /// <summary>TODO: Should be configurable?</summary>
        internal int BatchDownloadSize = 1000;

        private TimeSpan pushInterval = TimeSpan.FromSeconds(10);

        internal readonly TimeSpan PushIntervalIBD = TimeSpan.FromMilliseconds(100);

        /// <summary>Public constructor for unit testing</summary>
        public BlockStoreLoop(IAsyncLoopFactory asyncLoopFactory,
            StoreBlockPuller blockPuller,
            IBlockRepository blockRepository,
            IBlockStoreCache cache,
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
            this.nodeLifetime = nodeLifetime;
            this.nodeArgs = nodeArgs;
            this.storeLogger = loggerFactory.CreateLogger(GetType().FullName);

            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, cache, this.storeLogger);
        }

        /// <summary>
        /// Initialize the BlockStore
        /// <para>
        /// If StoreTip is <c>null</c>, the store is out of sync. This can happen when:</para>
        /// <list>
        ///     <item>1. The node crashed.</item>
        ///     <item>2. The node was not closed down properly.</item>
        /// </list>
        /// <para>
        /// To recover we walk back the chain until a common block header is found 
        /// and set the BlockStore's StoreTip to that.
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
                this.storeLogger.LogWarning($"{this.StoreName} Initialize recovering to block height = {newTip.Height} hash = {newTip.HashBlock}");
            }

            if (this.nodeArgs.Store.TxIndex != this.BlockRepository.TxIndex)
            {
                if (this.StoreTip != this.Chain.Genesis)
                    throw new BlockStoreException($"You need to rebuild the {this.StoreName} database using -reindex-chainstate to change -txindex");
                if (this.nodeArgs.Store.TxIndex)
                    await this.BlockRepository.SetTxIndex(this.nodeArgs.Store.TxIndex);
            }

            SetHighestPersistedBlock(this.StoreTip);

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
        /// The BlockStoreSignaler calls AddToPending. Only add the block to pending storage if:
        /// </para>
        /// <list>
        ///     <item>1: The block does exist on the chain.</item>
        ///     <item>2: The store's tip is behind the given block.</item>
        /// </list>
        /// </summary>
        /// <param name="block">The block to add to pending storage</param>
        /// <remarks>TODO: Possibly check the size of pending in memory</remarks>
        public void AddToPending(Block block)
        {
            ChainedBlock chainedBlock = this.Chain.GetBlock(block.GetHash());
            if (chainedBlock == null)
                return;

            if (this.StoreTip.Height < chainedBlock.Height)
                this.PendingStorage.TryAdd(chainedBlock.HashBlock, new BlockPair(block, chainedBlock));
        }

        ///<summary>Persists unsaved blocks to disk when the node shuts down.</summary>
        public Task Flush()
        {
            return DownloadAndStoreBlocks(CancellationToken.None, true);
        }

        /// <summary>
        /// Executes DownloadAndStoreBlocks()
        /// </summary>
        internal void StartLoop()
        {
            this.asyncLoopFactory.Run($"{this.StoreName}.DownloadAndStoreBlocks", async token =>
                {
                    await DownloadAndStoreBlocks(this.nodeLifetime.ApplicationStopping);
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpans.Second,
                startAfter: TimeSpans.FiveSeconds);
        }

        /// <summary>
        /// Finds and downloads blocks to store in the BlockRepository.
        /// <para>
        /// This method executes a chain of steps in order:
        /// <list>
        ///     <item>1. Reorganise the repository</item>
        ///     <item>2. Check if the block exists in store, if it does move on to the next block</item>
        ///     <item>3. Process the blocks in pending storage</item>
        ///     <item>4. Find and download blocks</item>
        /// </list>
        /// </para>
        /// <para>
        /// Steps return a <see cref="StepResult"/> which either signals the While loop
        /// to break or continue execution.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">CancellationToken to check</param>
        /// <param name="disposeMode">This will <c>true</c> if the Flush() was called</param>
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

                var nextChainedBlock = this.Chain.GetBlock(this.StoreTip.Height + 1);
                if (nextChainedBlock == null)
                    break;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                var result = await this.stepChain.Execute(nextChainedBlock, disposeMode, cancellationToken);
                if (result == StepResult.Stop)
                    break;
                if (result == StepResult.Continue)
                    continue;
            }
        }

        /// <summary>Set the store's tip</summary>
        internal void SetStoreTip(ChainedBlock chainedBlock)
        {
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.StoreTip = chainedBlock;

            SetHighestPersistedBlock(chainedBlock);
        }

        protected virtual void SetHighestPersistedBlock(ChainedBlock block)
        {
            this.ChainState.HighestPersistedBlock = block;
        }
    }
}