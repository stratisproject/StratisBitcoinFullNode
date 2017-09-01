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
        /// <summary>Maximum number of bytes the block puller can download before the downloaded blocks are stored to the disk.</summary>
        internal const uint MaxInsertBlockSize = 20 * 1024 * 1024;

        /// <summary>Maximum number of bytes the pending storage can hold until the downloaded blocks are stored to the disk.</summary>
        internal const uint MaxPendingInsertBlockSize = 5 * 1000 * 1000;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary> Best chain of block headers.</summary>
        internal readonly ConcurrentChain Chain;

        public StoreBlockPuller BlockPuller { get; }
        public IBlockRepository BlockRepository { get; }
        public virtual string StoreName { get { return "BlockStore"; } }

        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly BlockStoreStats blockStoreStats;
        private readonly NodeSettings nodeArgs;
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>The chain of steps that gets executed to find and download blocks.</summary>
        private BlockStoreStepChain stepChain;

        public ChainState ChainState { get; }

        /// <summary>Blocks that in PendingStorage will be processed first before new blocks are downloaded.</summary>
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

        /// <summary>Number of blocks that can be stored in pending storage before we stop processing them.</summary>
        internal int PendingStorageBatchThreshold = 5;

        /// <summary>The highest stored block in the repository.</summary>
        internal ChainedBlock StoreTip { get; private set; }

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
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.BlockPuller = blockPuller;
            this.BlockRepository = blockRepository;
            this.Chain = chain;
            this.ChainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.nodeArgs = nodeArgs;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.dateTimeProvider = dateTimeProvider;

            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, cache, this.logger);
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
            this.logger.LogTrace("()");

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
                this.logger.LogWarning("{0} Initialize recovering to block height = {1}, hash = {2}.", this.StoreName, newTip.Height, newTip.HashBlock);
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
            this.stepChain.SetNextStep(new ReorganiseBlockRepositoryStep(this, this.loggerFactory));
            this.stepChain.SetNextStep(new CheckNextChainedBlockExistStep(this, this.loggerFactory));
            this.stepChain.SetNextStep(new ProcessPendingStorageStep(this, this.loggerFactory));
            this.stepChain.SetNextStep(new DownloadBlockStep(this, this.loggerFactory, this.dateTimeProvider));

            StartLoop();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds a block to Pending Storage
        /// <para>
        /// The <see cref="BlockStoreSignaled"/> calls this method when a new block is available. Only add the block to pending storage if:
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
            uint256 blockHash = block.GetHash();
            this.logger.LogTrace("({0}:'{1}')", nameof(block), blockHash);

            ChainedBlock chainedBlock = this.Chain.GetBlock(blockHash);
            if (chainedBlock == null)
            {
                this.logger.LogTrace("(-)[REORG]");
                return;
            }

            if (this.StoreTip.Height < chainedBlock.Height)
                this.PendingStorage.TryAdd(chainedBlock.HashBlock, new BlockPair(block, chainedBlock));

            this.logger.LogTrace("(-)");
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
            this.logger.LogTrace("()");

            this.asyncLoopFactory.Run($"{this.StoreName}.DownloadAndStoreBlocks", async token =>
                {
                    this.logger.LogTrace("()");

                    await DownloadAndStoreBlocks(this.nodeLifetime.ApplicationStopping);

                    this.logger.LogTrace("(-)");
                },
                this.nodeLifetime.ApplicationStopping,
                repeatEvery: TimeSpans.Second,
                startAfter: TimeSpans.FiveSeconds);

            this.logger.LogTrace("(-)");
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
            this.logger.LogTrace("({0}:{1})", nameof(disposeMode), disposeMode);

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
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the store's tip</summary>
        internal void SetStoreTip(ChainedBlock chainedBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedBlock), chainedBlock?.HashBlock);
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.StoreTip = chainedBlock;

            SetHighestPersistedBlock(chainedBlock);

            this.logger.LogTrace("(-)");
        }

        protected virtual void SetHighestPersistedBlock(ChainedBlock block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block?.HashBlock);

            this.ChainState.HighestPersistedBlock = block;

            this.logger.LogTrace("(-)");
        }
    }
}