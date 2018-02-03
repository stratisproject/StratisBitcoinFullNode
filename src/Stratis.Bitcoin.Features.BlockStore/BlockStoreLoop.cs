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
    /// The BlockStoreLoop stores blocks downloaded by <see cref="LookaheadBlockPuller"/> to the BlockRepository.
    /// </summary>
    public class BlockStoreLoop : IDisposable
    {
        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        private Task storeBlocksTask;
        
        public IBlockRepository BlockRepository { get; }

        private readonly BlockStoreStats blockStoreStats;

        /// <summary>Thread safe access to the best chain of block headers (that the node is aware of) from genesis.</summary>
        internal readonly ConcurrentChain Chain;

        /// <summary>Provider of IBD state.</summary>
        public IInitialBlockDownloadState InitialBlockDownloadState { get; }

        public IChainState ChainState { get; }
        
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;
        
        /// <summary>Maximum number of bytes the pending storage can hold until the downloaded blocks are stored to the disk.</summary>
        public const uint MaxPendingInsertBlockSize = 5 * 1024 * 1024;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Blocks that in PendingStorage will be processed first before new blocks are downloaded.</summary>
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }

        /// <summary>The minimum amount of blocks that can be stored in Pending Storage before they get processed.</summary>
        public const int PendingStorageBatchThreshold = 10;

        public virtual string StoreName
        {
            get { return "BlockStore"; }
        }

        private readonly StoreSettings storeSettings;

        private ProcessPendingStorageStep processPendingStorageStep;

        private readonly AsyncManualResetEvent blockProcessingRequestedTrigger;

        /// <summary>The highest stored block in the repository.</summary>
        internal ChainedBlock StoreTip { get; private set; }

        /// <summary>Public constructor for unit testing.</summary>
        public BlockStoreLoop(IAsyncLoopFactory asyncLoopFactory,
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
            this.BlockRepository = blockRepository;
            this.Chain = chain;
            this.ChainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.storeSettings = storeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
            this.InitialBlockDownloadState = initialBlockDownloadState;

            this.PendingStorage = new ConcurrentDictionary<uint256, BlockPair>();
            this.blockStoreStats = new BlockStoreStats(this.BlockRepository, cache, dateTimeProvider, this.logger);

            this.blockProcessingRequestedTrigger = new AsyncManualResetEvent(false);
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

                ChainedBlock newTip = this.Chain.GetBlock(resetBlockHash);
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

            this.processPendingStorageStep = new ProcessPendingStorageStep(this, this.loggerFactory);

            this.StartSavingBlocksToTheRepository();

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
            this.logger.LogTrace("({0}:'{1}')", nameof(blockPair), blockPair.ChainedBlock);

            if (this.StoreTip.Height < blockPair.ChainedBlock.Height)
            {
                this.PendingStorage.TryAdd(blockPair.ChainedBlock.HashBlock, blockPair);
                this.blockProcessingRequestedTrigger.Set();
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
        public void Dispose()
        {
            this.storeBlocksTask?.Wait();
        }

        /// <summary>Executes <see cref="StoreBlocksAsync"/>.</summary>
        private void StartSavingBlocksToTheRepository()
        {
            this.storeBlocksTask = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpans.FiveSeconds, this.nodeLifetime.ApplicationStopping);

                    await this.StoreBlocksAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }

        /// <summary>
        /// Saves blocks from <see cref="PendingStorage"/> to the <see cref="BlockRepository"/>.
        /// <para>
        /// This method executes a chain of steps in order:
        /// <list>
        ///     <item>1. Reorganise the repository.</item>
        ///     <item>2. Check if the block exists in store, if it does move on to the next block.</item>
        ///     <item>3. Process the blocks in pending storage.</item>
        /// </list>
        /// </para>
        /// <para>
        /// Steps return a <see cref="StepResult"/> which either signals the While loop
        /// to break or continue execution.
        /// </para>
        /// </summary>
        /// <param name="cancellationToken">CancellationToken to check</param>
        private async Task StoreBlocksAsync(CancellationToken cancellationToken)
        {
            bool disposeMode = false;

            // TODO handle here a scenario when consensus tip is ahead of the block store tip. Node wasn't shutted down properly last time and we need to download missing blocks.

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    if (!disposeMode)
                        // Force this iteration to flush.
                        disposeMode = true;
                    else
                        break;
                }
                
                // Wait for signal before continuing.
                await this.blockProcessingRequestedTrigger.WaitAsync(cancellationToken).ConfigureAwait(false);
                this.blockProcessingRequestedTrigger.Reset();
                
                ChainedBlock nextChainedBlock = this.Chain.GetBlock(this.StoreTip.Height + 1);
                if (nextChainedBlock == null)
                    continue;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                //TODO remove step results

                // Reorganize the repository if needed. If not- continue.
                if (await this.TryReorganiseBlockRepositoryAsync(nextChainedBlock, disposeMode).ConfigureAwait(false))
                    break;
                
                // Check if block was already saved to the BlockRepository.
                if (await this.BlockRepository.ExistAsync(nextChainedBlock.HashBlock))
                {
                    await this.BlockRepository.SetBlockHashAsync(nextChainedBlock.HashBlock);

                    this.SetStoreTip(nextChainedBlock);

                    this.logger.LogTrace("(-)[EXIST]");
                    continue;
                }

                StepResult result = await this.processPendingStorageStep.ExecuteAsync(nextChainedBlock, cancellationToken, disposeMode).ConfigureAwait(false);
                if (result == StepResult.Stop)
                    continue;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reorganises the <see cref="BlockStore.BlockRepository"/>.
        /// <para>
        /// This will happen when the block store's tip does not match
        /// the next chained block's previous header.
        /// </para>
        /// <para>
        /// Steps:
        /// <list type="bullet">
        ///     <item>1: Add blocks to delete from the repository by walking back the chain until the last chained block is found.</item>
        ///     <item>2: Delete those blocks from the BlockRepository.</item>
        ///     <item>3: Set the last stored block (tip) to the last found chained block.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <returns>
        /// If the store/repository does not require reorganising the step will return <see cref="StepResult.Next"/>. 
        /// If not- it will return <see cref="StepResult.Stop"/> which will cause the <see cref="BlockStoreLoop" /> to break execution and start again.
        /// </returns>
        private async Task<bool> TryReorganiseBlockRepositoryAsync(ChainedBlock nextChainedBlock, bool disposeMode)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(nextChainedBlock), nextChainedBlock, nameof(disposeMode), disposeMode);

            if (this.StoreTip.HashBlock != nextChainedBlock.Header.HashPrevBlock)
            {
                if (disposeMode)
                {
                    this.logger.LogTrace("(-)[DISPOSE]");
                    return true;
                }

                var blocksToDelete = new List<uint256>();
                ChainedBlock blockToDelete = this.StoreTip;

                while (this.Chain.GetBlock(blockToDelete.HashBlock) == null)
                {
                    blocksToDelete.Add(blockToDelete.HashBlock);
                    blockToDelete = blockToDelete.Previous;
                }

                await this.BlockRepository.DeleteAsync(blockToDelete.HashBlock, blocksToDelete);

                this.SetStoreTip(blockToDelete);

                this.logger.LogTrace("(-)[MISMATCH]");
                return true;
            }

            this.logger.LogTrace("(-)");
            return false;
        }
        
        /// <summary>Set the store's tip</summary>
        internal void SetStoreTip(ChainedBlock chainedBlock)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedBlock), chainedBlock?.HashBlock);
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.StoreTip = chainedBlock;
            this.SetHighestPersistedBlock(chainedBlock);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the highest persisted block in the chain.</summary>
        private void SetHighestPersistedBlock(ChainedBlock block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block?.HashBlock);

            if (this.BlockRepository is BlockRepository blockRepository)
                blockRepository.HighestPersistedBlock = block;

            this.logger.LogTrace("(-)");
        }
    }
}
