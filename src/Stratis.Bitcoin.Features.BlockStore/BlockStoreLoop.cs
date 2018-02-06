using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// The BlockStoreLoop stores blocks downloaded by <see cref="LookaheadBlockPuller"/> to the BlockRepository.
    /// </summary>
    public class BlockStoreLoop : IDisposable
    {
        private Task storeBlocksTask;

        private StoreBlockPuller BlockPuller { get; }

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
        
        /// <summary>Target number of bytes the pending storage should hold until the downloaded blocks are stored to the disk.</summary>
        public const int TargetPendingInsertSize = 2 * 1024 * 1024;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        private readonly INodeLifetime nodeLifetime;

        /// <summary>Blocks that in PendingStorage will be processed first before new blocks are downloaded.</summary>
        public ConcurrentDictionary<uint256, BlockPair> PendingStorage { get; }
        
        /// <summary>Maximum number of milliseconds to get a block from the block puller before reducing quality score of the peers that owe us blocks.</summary>
        public const int StallDelayMs = 200;

        public virtual string StoreName
        {
            get { return "BlockStore"; }
        }

        private readonly StoreSettings storeSettings;

        private PendingStorageProcessor pendingStorageProcessor;

        private readonly AsyncManualResetEvent blockProcessingRequestedTrigger;

        /// <summary>The highest stored block in the repository.</summary>
        internal ChainedBlock StoreTip { get; private set; }
        
        /// <summary>Represents the last block stored to disk.</summary>
        public ChainedBlock HighestPersistedBlock { get; private set; }

        /// <summary>Creates new instance of <see cref="BlockStoreLoop"/>.</summary>
        public BlockStoreLoop(StoreBlockPuller blockPuller,
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
            this.BlockPuller = blockPuller;
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
        /// Initializes the BlockStore.
        /// <para>
        /// If StoreTip is <c>null</c>, the store is out of sync. This can happen if the node has crashed or was not closed down properly.
        /// </para>
        /// <para>
        /// To recover we walk back the chain until a common block header is found and set the BlockStore's StoreTip to that.
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
                    throw new Exception($"You need to rebuild the {this.StoreName} database using -reindex-chainstate to change -txindex");

                if (this.storeSettings.TxIndex)
                    await this.BlockRepository.SetTxIndexAsync(this.storeSettings.TxIndex).ConfigureAwait(false);
            }

            this.HighestPersistedBlock = this.StoreTip;

            this.pendingStorageProcessor = new PendingStorageProcessor(this, this.loggerFactory);

            this.StartSavingBlocksToTheRepository();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds a block to Pending Storage.
        /// <para>
        /// The <see cref="BlockStoreSignaled"/> calls this method when a new block is available.
        /// </para>
        /// </summary>
        /// <param name="blockPair">The block and its chained header pair to be added to pending storage.</param>
        public void AddToPending(BlockPair blockPair)
        {
            this.logger.LogTrace("() New block was received:'{0}'", blockPair.ChainedBlock);

            if (this.StoreTip.Height < blockPair.ChainedBlock.Height)
            {
                this.PendingStorage.TryAdd(blockPair.ChainedBlock.HashBlock, blockPair);
                this.blockProcessingRequestedTrigger.Set();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Executes <see cref="StoreBlocksAsync"/>.</summary>
        private void StartSavingBlocksToTheRepository()
        {
            this.storeBlocksTask = Task.Run(async () =>
            {
                await this.StoreBlocksAsync().ConfigureAwait(false);
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
        /// </summary>
        private async Task StoreBlocksAsync()
        {
            this.logger.LogTrace("()");

            bool disposeMode = false;

            while (true)
            {
                if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                {
                    if (!disposeMode)
                        // Force this iteration to flush.
                        disposeMode = true;
                    else
                        break;
                }

                this.blockProcessingRequestedTrigger.Reset();
                if (this.StoreTip.Height >= this.ChainState.ConsensusTip?.Height)
                {
                    this.logger.LogDebug("Store Tip has reached the Consensus Tip. Waiting for new block.");

                    try
                    {
                        await this.blockProcessingRequestedTrigger.WaitAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Carry on to the next iteration that will be ran in dispose mode.
                        continue;
                    }
                }
                
                ChainedBlock nextChainedBlock = this.Chain.GetBlock(this.StoreTip.Height + 1);
                if (nextChainedBlock == null)
                    continue;

                if (this.blockStoreStats.CanLog)
                    this.blockStoreStats.Log();

                // Reorganize the repository if needed.
                if (await this.TryReorganiseBlockRepositoryAsync(nextChainedBlock, disposeMode).ConfigureAwait(false))
                    continue;
                
                // Check if block was already saved to the BlockRepository.
                if (await this.BlockRepository.ExistAsync(nextChainedBlock.HashBlock).ConfigureAwait(false))
                {
                    await this.BlockRepository.SetBlockHashAsync(nextChainedBlock.HashBlock).ConfigureAwait(false);

                    this.SetStoreTip(nextChainedBlock);

                    this.logger.LogDebug("Block {0} already exist in the repository.", nextChainedBlock);
                    continue;
                }

                // Check if next block exist in pending storage.
                if (this.PendingStorage.ContainsKey(nextChainedBlock.HashBlock))
                {
                    this.blockProcessingRequestedTrigger.Reset();

                    this.logger.LogTrace("Processing pending storage.");
                    await this.pendingStorageProcessor.ExecuteAsync(nextChainedBlock, this.nodeLifetime.ApplicationStopping.IsCancellationRequested).ConfigureAwait(false);

                    this.logger.LogTrace("Pending storage processing finished, waiting for a new block.");

                    // Wait for next block.
                    try
                    {
                        if (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                            await this.blockProcessingRequestedTrigger.WaitAsync(this.nodeLifetime.ApplicationStopping).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Carry on to the next iteration that will be ran in dispose mode.
                        continue;
                    }
                }
                else if (!disposeMode)
                {
                    // Consensus tip is ahead of the block store tip and the pending storage. 
                    // This is only possible if node wasn't shutted down properly last time so we need to download the missing blocks.
                    List<ChainedBlock> missing = await this.FindMissingBlocksAsync(nextChainedBlock).ConfigureAwait(false);
                    this.logger.LogDebug("At least {0} blocks are missing in the repository. Start downloading them.", missing.Count);
                    await this.DownloadBlocksAsync(missing).ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Reorganises the <see cref="BlockStore.BlockRepository"/>.
        /// <para>
        /// This will happen when the block store's tip does not match the next chained block's previous header.
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
        /// If the store/repository did not require reorganising <c>false</c> will be returned. Otherwise: <c>true</c>.
        /// </returns>
        internal async Task<bool> TryReorganiseBlockRepositoryAsync(ChainedBlock nextChainedBlock, bool disposeMode)
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

                await this.BlockRepository.DeleteAsync(blockToDelete.HashBlock, blocksToDelete).ConfigureAwait(false);

                this.SetStoreTip(blockToDelete);

                this.logger.LogTrace("(-)[MISMATCH]");
                return true;
            }

            this.logger.LogTrace("(-)");
            return false;
        }

        /// <summary>
        /// Finds blocks that are missing in the repository on a range between StorageTip and first block that exist in the <see cref="PendingStorage"/>.
        /// </summary>
        /// <param name="firstMissingBlock">First block that does not exist in the <see cref="PendingStorage"/>.</param>
        /// <param name="maxCount">Upper limit of blocks that should be returned.</param>
        private async Task<List<ChainedBlock>> FindMissingBlocksAsync(ChainedBlock firstMissingBlock, int maxCount = 10)
        {
            var missedBlocks = new List<ChainedBlock>() { firstMissingBlock };

            for (int i = 0; i < maxCount - 1; i++)
            {
                ChainedBlock currentBlock = this.Chain.GetBlock(missedBlocks.Last().Height + 1);
                if (currentBlock == null || this.PendingStorage.ContainsKey(currentBlock.HashBlock))
                    break;

                if (await this.BlockRepository.ExistAsync(currentBlock.HashBlock).ConfigureAwait(false))
                    break;

                missedBlocks.Add(currentBlock);
            }

            return missedBlocks;
        }

        /// <summary>Schedules download for specified blocks and waits for it to be finished.</summary>
        private async Task DownloadBlocksAsync(List<ChainedBlock> blocks)
        {
            this.logger.LogTrace("()");

            this.BlockPuller.AskForMultipleBlocks(blocks.ToArray());

            while (blocks.Count > 0 && !this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                var timeoutSource = new CancellationTokenSource(StallDelayMs);

                try
                {
                    BlockPuller.DownloadedBlock downloadedBlock = await this.BlockPuller.GetNextDownloadedBlockAsync(timeoutSource.Token).ConfigureAwait(false);

                    ChainedBlock current = blocks.FirstOrDefault(x => x.HashBlock == downloadedBlock.Block.GetHash());

                    if (current != null)
                    {
                        this.logger.LogTrace("Puller provided block '{0}', length {1}.", current, downloadedBlock.Length);

                        this.AddToPending(new BlockPair(downloadedBlock.Block, current));
                        blocks.Remove(current);
                    }
                    else
                    {
                        this.logger.LogTrace("Puller provided block that was not requested! '{0}', length {1}.", downloadedBlock.Block.GetHash(), downloadedBlock.Length);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Do nothing if application is stopping.
                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                        return;

                    foreach (ChainedBlock block in blocks)
                        this.BlockPuller.Stall(block);
                }
                finally
                {
                    timeoutSource.Dispose();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the store's tip</summary>
        internal void SetStoreTip(ChainedBlock chainedBlock)
        {
            Guard.NotNull(chainedBlock, nameof(chainedBlock));

            this.StoreTip = chainedBlock;
            this.HighestPersistedBlock = chainedBlock;

            this.logger.LogTrace("Store Tip was set to '{0}'", chainedBlock);
        }

        /// <summary>Persists unsaved blocks to disk when the node shuts down.</summary>
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.storeBlocksTask?.Wait();
            this.BlockPuller.Dispose();

            this.logger.LogTrace("(-)");
        }
    }
}
