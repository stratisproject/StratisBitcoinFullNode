using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Features.BlockStore.LoopSteps;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The BlockStoreLoop simultaneously finds and downloads blocks and stores them in the BlockRepository.
    /// </summary>
    public class BlockStoreLoop : IStoreStateProvider
    {
        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        public StoreBlockPuller BlockPuller { get; }

        public IBlockRepository BlockRepository { get; }

        private readonly BlockStoreStats blockStoreStats;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        internal readonly ConcurrentChain Chain;

        /// <summary>Provider of IBD state.</summary>
        public IInitialBlockDownloadState InitialBlockDownloadState { get; }

        public IChainState ChainState { get; }

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>Maximum number of bytes the block puller can download before the downloaded blocks are stored to the disk.</summary>
        internal const uint MaxInsertBlockSize = 20 * 1024 * 1024;

        /// <summary>Maximum number of bytes the pending storage can hold until the downloaded blocks are stored to the disk.</summary>
        internal const uint MaxPendingInsertBlockSize = 5 * 1000 * 1000;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Blocks that in PendingStorage will be processed first before new blocks are downloaded.</summary>
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

        /// <summary>The minimum amount of blocks that can be stored in Pending Storage before they get processed.</summary>
        public const int PendingStorageBatchThreshold = 10;

        /// <summary>The chain of steps that gets executed to find and download blocks.</summary>
        private BlockStoreStepChain stepChain;

        /// <summary>Cached consensus tip.</summary>
        /// <remarks>
        /// Cached tip is needed in order to avoid race condition in the <see cref="DownloadAndStoreBlocksAsync"/>.
        /// <para>
        /// This condition happens when the actual ConsensusTip is updated but the block wasn't provided by signaler yet.
        /// </para>
        /// <para>
        /// TODO: remove this quick fix later and solve the race condition by replacing the async loop with trigger-based invoking of <see cref="DownloadAndStoreBlocksAsync"/>.
        /// </para>
        /// </remarks>
        private ChainedHeader CachedConsensusTip;

        public virtual string StoreName
        {
            get { return "BlockStore"; }
        }

        private readonly StoreSettings storeSettings;

        /// <summary>The highest stored block in the repository.</summary>
        public ChainedHeader StoreTip { get; private set; }

        /// <summary>Public constructor for unit testing.</summary>
        public BlockStoreLoop(IAsyncLoopFactory asyncLoopFactory,
            StoreBlockPuller blockPuller,
            IBlockRepository blockRepository,
            IBlockStoreCache cache,
            ConcurrentChain chain,
            IChainState chainState,
            StoreSettings storeSettings,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            IInitialBlockDownloadState initialBlockDownloadState,
            IDateTimeProvider dateTimeProvider)
        {
            this.asyncLoopFactory = asyncLoopFactory;
            this.BlockPuller = blockPuller;
            this.BlockRepository = blockRepository;
            this.Chain = chain;
            this.ChainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.storeSettings = storeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.dateTimeProvider = dateTimeProvider;
            this.InitialBlockDownloadState = initialBlockDownloadState;

            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, cache, this.dateTimeProvider, this.logger);
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
        public async Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            if (this.storeSettings.ReIndex)
                throw new NotImplementedException();

            this.StoreTip = this.Chain.GetBlock(this.BlockRepository.BlockHash);

            if (this.StoreTip == null)
            {
                var blockStoreResetList = new List<uint256>();
                Block resetBlock = await this.BlockRepository.GetAsync(this.BlockRepository.BlockHash).ConfigureAwait(false);
                uint256 resetBlockHash = resetBlock.GetHash();

                while (this.Chain.GetBlock(resetBlockHash) == null)
                {
                    blockStoreResetList.Add(resetBlockHash);

                    if (resetBlock.Header.HashPrevBlock == this.Chain.Genesis.HashBlock)
                    {
                        resetBlockHash = this.Chain.Genesis.HashBlock;
                        break;
                    }

                    resetBlock = await this.BlockRepository.GetAsync(resetBlock.Header.HashPrevBlock).ConfigureAwait(false);
                    Guard.NotNull(resetBlock, nameof(resetBlock));
                    resetBlockHash = resetBlock.GetHash();
                }

                ChainedHeader newTip = this.Chain.GetBlock(resetBlockHash);
                await this.BlockRepository.DeleteAsync(newTip.HashBlock, blockStoreResetList).ConfigureAwait(false);
                this.StoreTip = newTip;
                this.logger.LogWarning("{0} Initialize recovering to block height = {1}, hash = {2}.", this.StoreName, newTip.Height, newTip.HashBlock);
            }

            if (this.storeSettings.TxIndex != this.BlockRepository.TxIndex)
            {
                if (this.StoreTip != this.Chain.Genesis)
                    throw new BlockStoreException($"You need to rebuild the {this.StoreName} database using -reindex-chainstate to change -txindex");

                if (this.storeSettings.TxIndex)
                    await this.BlockRepository.SetTxIndexAsync(this.storeSettings.TxIndex).ConfigureAwait(false);
            }

            this.SetHighestPersistedBlock(this.StoreTip);

            this.stepChain = new BlockStoreStepChain();
            this.stepChain.SetNextStep(new ReorganiseBlockRepositoryStep(this, this.loggerFactory));
            this.stepChain.SetNextStep(new CheckNextChainedBlockExistStep(this, this.loggerFactory));
            this.stepChain.SetNextStep(new ProcessPendingStorageStep(this, this.loggerFactory));
            this.stepChain.SetNextStep(new DownloadBlockStep(this, this.loggerFactory, this.dateTimeProvider));

            this.CachedConsensusTip = this.ChainState.ConsensusTip;

            this.StartLoop();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds a block to Pending Storage.
        /// <para>
        /// The <see cref="BlockStoreSignaled"/> calls this method when a new block is available. Only add the block to pending storage if the store's tip is behind the given block.
        /// </para>
        /// </summary>
        /// <param name="blockPair">The block and its chained header pair to be added to pending storage.</param>
        /// <remarks>TODO: Possibly check the size of pending in memory</remarks>
        public void AddToPending(BlockPair blockPair)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockPair), blockPair.ChainedHeader);

            if (this.StoreTip.Height < blockPair.ChainedHeader.Height)
            {
                this.PendingStorage.TryAdd(blockPair.ChainedHeader.HashBlock, blockPair);
                this.CachedConsensusTip = blockPair.ChainedHeader;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Persists unsaved blocks to disk when the node shuts down.
        /// <para>
        /// Before we can shut down we need to ensure that the current async loop
        /// has completed.
        /// </para>
        /// </summary>
        internal void ShutDown()
        {
            this.asyncLoop?.Dispose();
            this.DownloadAndStoreBlocksAsync(CancellationToken.None, true).Wait();
        }

        /// <summary>
        /// Executes DownloadAndStoreBlocks()
        /// </summary>
        private void StartLoop()
        {
            this.asyncLoop = this.asyncLoopFactory.Run($"{this.StoreName}.DownloadAndStoreBlocks", async token =>
            {
                await this.DownloadAndStoreBlocksAsync(this.nodeLifetime.ApplicationStopping, false).ConfigureAwait(false);
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
        private async Task DownloadAndStoreBlocksAsync(CancellationToken cancellationToken, bool disposeMode)
        {
            this.logger.LogTrace("({0}:{1})", nameof(disposeMode), disposeMode);

            while (!cancellationToken.IsCancellationRequested)
            {
                if (this.StoreTip.Height >= this.CachedConsensusTip?.Height)
                    break;

                var nextChainedBlock = this.Chain.GetBlock(this.StoreTip.Height + 1);
                if (nextChainedBlock == null)
                    break;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                var result = await this.stepChain.ExecuteAsync(nextChainedBlock, disposeMode, cancellationToken).ConfigureAwait(false);
                if (result == StepResult.Stop)
                    break;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the store's tip</summary>
        internal void SetStoreTip(ChainedHeader chainedHeader)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedHeader), chainedHeader?.HashBlock);
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            this.StoreTip = chainedHeader;
            this.SetHighestPersistedBlock(chainedHeader);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the highest persisted block in the chain.</summary>
        protected virtual void SetHighestPersistedBlock(ChainedHeader block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block?.HashBlock);

            if (this.BlockRepository is BlockRepository blockRepository)
                blockRepository.HighestPersistedBlock = block;

            this.logger.LogTrace("(-)");
        }
    }
}
