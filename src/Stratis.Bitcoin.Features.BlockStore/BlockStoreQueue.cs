using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Saves blocks to the database in batches, removes reorged blocks from the database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The batch is saved when total serialized size of all blocks in a batch reaches <see cref="BatchThresholdSizeBytes"/>,
    /// or when more than <see cref="BatchMaxSaveIntervalSeconds"/> passed since last batch was saved, or when node is shutting down.
    /// </para>
    /// <para>
    /// When we save new blocks to the database, in case <see cref="IBlockRepository"/> contains blocks that
    /// are no longer a part of our best chain, they are removed from the database.
    /// </para>
    /// <para>
    /// When block store is being initialized we delete blocks that are not on the best chain.
    /// </para>
    /// </remarks>
    public class BlockStoreQueue : IDisposable
    {
        /// <summary>Maximum interval between saving batches.</summary>
        /// <remarks>Interval value is a prime number that wasn't used as an interval in any other component. That prevents having CPU consumption spikes.</remarks>
        private const int BatchMaxSaveIntervalSeconds = 37;

        /// <summary>Maximum number of bytes the batch can hold until the downloaded blocks are stored to the disk.</summary>
        internal const int BatchThresholdSizeBytes = 5 * 1000 * 1000;

        /// <summary>The current batch size in bytes.</summary>
        private int currentBatchSizeBytes;

        /// <summary>The highest stored block in the repository.</summary>
        private ChainedHeader storeTip;
        
        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        /// <inheritdoc cref="INodeLifetime"/>
        private readonly INodeLifetime nodeLifetime;

        /// <inheritdoc cref="IChainState"/>
        private readonly IChainState chainState;

        /// <inheritdoc cref="StoreSettings"/>
        private readonly StoreSettings storeSettings;

        /// <inheritdoc cref="ConcurrentChain"/>
        private readonly ConcurrentChain chain;

        /// <inheritdoc cref="IBlockRepository"/>
        private readonly IBlockRepository blockRepository;

        /// <summary>Queue which contains blocks that should be saved to the database.</summary>
        private readonly AsyncQueue<BlockPair> blocksQueue;

        /// <summary>Task that runs <see cref="DequeueBlocksContinuouslyAsync"/>.</summary>
        private Task dequeueLoopTask;

        public BlockStoreQueue(
            IBlockRepository blockRepository,
            ConcurrentChain chain,
            IChainState chainState,
            StoreSettings storeSettings,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory)
        {
            Guard.NotNull(blockRepository, nameof(blockRepository));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(nodeLifetime, nameof(nodeLifetime));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            
            this.chainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.storeSettings = storeSettings;
            this.chain = chain;
            this.blockRepository = blockRepository;

            this.blocksQueue = new AsyncQueue<BlockPair>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Initializes the <see cref="BlockStoreQueue"/>.
        /// <para>
        /// If <see cref="storeTip"/> is <c>null</c>, the store is out of sync. This can happen when:</para>
        /// <list>
        ///     <item>The node crashed.</item>
        ///     <item>The node was not closed down properly.</item>
        /// </list>
        /// <para>
        /// To recover we walk back the chain until a common block header is found and set the <see cref="BlockStoreQueue"/>'s <see cref="storeTip"/> to that.
        /// </para>
        /// </summary>
        public async Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            if (this.storeSettings.ReIndex)
            {
                this.logger.LogTrace("(-)[REINDEX_NOT_SUPPORTED]");
                throw new NotSupportedException("Re-indexing the block store in currently not supported.");
            }

            ChainedHeader initializationTip = this.chain.GetBlock(this.blockRepository.BlockHash);
            this.SetStoreTip(initializationTip);
            
            if (this.storeTip == null)
                await this.RecoverStoreTipAsync().ConfigureAwait(false);

            this.logger.LogDebug("Initialized block store tip at '{0}'.", this.storeTip);

            if (this.storeSettings.TxIndex != this.blockRepository.TxIndex)
            {
                if (this.storeTip != this.chain.Genesis)
                {
                    this.logger.LogTrace("(-)[REBUILD_REQUIRED]");
                    throw new BlockStoreException("You need to rebuild the block store database using -reindex-chainstate to change -txindex");
                }

                if (this.storeSettings.TxIndex)
                    await this.blockRepository.SetTxIndexAsync(this.storeSettings.TxIndex).ConfigureAwait(false);
            }
            
            // Throw if block store was initialized after the consensus.
            // This is needed in order to rewind consensus in case it is initialized ahead of the block store.
            if (this.chainState.ConsensusTip != null)
            {
                this.logger.LogCritical("Block store initialized after the consensus!");
                this.logger.LogTrace("(-)[INITIALIZATION_ERROR]");
                throw new BlockStoreException("Block store initialized after consensus!");
            }

            // Start dequeuing.
            this.currentBatchSizeBytes = 0;
            this.dequeueLoopTask = this.DequeueBlocksContinuouslyAsync();

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>Sets the internal store tip and exposes the store tip to other components through the chain state.</summary>
        private void SetStoreTip(ChainedHeader newTip)
        {
            this.storeTip = newTip;
            this.chainState.BlockStoreTip = newTip;
        }

        /// <summary>
        /// Sets block store tip to the last block that exists both in the repository and in the <see cref="ConcurrentChain"/>.
        /// </summary>
        private async Task RecoverStoreTipAsync()
        {
            this.logger.LogTrace("()");

            var blockStoreResetList = new List<uint256>();
            uint256 resetBlockHash = this.blockRepository.BlockHash;
            Block resetBlock = await this.blockRepository.GetAsync(resetBlockHash).ConfigureAwait(false);

            while (this.chain.GetBlock(resetBlockHash) == null)
            {
                blockStoreResetList.Add(resetBlockHash);

                if (resetBlock.Header.HashPrevBlock == this.chain.Genesis.HashBlock)
                {
                    resetBlockHash = this.chain.Genesis.HashBlock;
                    break;
                }

                resetBlock = await this.blockRepository.GetAsync(resetBlock.Header.HashPrevBlock).ConfigureAwait(false);

                if (resetBlock == null)
                {
                    // This can happen only if block store is corrupted.
                    throw new BlockStoreException("Block store failed to recover.");
                }

                resetBlockHash = resetBlock.GetHash();
            }

            ChainedHeader newTip = this.chain.GetBlock(resetBlockHash);

            if (blockStoreResetList.Count != 0)
                await this.blockRepository.DeleteAsync(newTip.HashBlock, blockStoreResetList).ConfigureAwait(false);

            this.SetStoreTip(newTip);

            this.logger.LogWarning("Block store tip recovered to block '{0}'.", newTip);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds a block to the saving queue.
        /// </summary>
        /// <param name="blockPair">The block and its chained header pair to be added to pending storage.</param>
        public void AddToPending(BlockPair blockPair)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(blockPair), blockPair.ChainedHeader);

            this.blocksQueue.Enqueue(blockPair);

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>
        /// Dequeues the blocks continuously and saves them to the database when max batch size is reached or timer ran out.
        /// </summary>
        /// <remarks>Batch is always saved on shutdown.</remarks>
        private async Task DequeueBlocksContinuouslyAsync()
        {
            this.logger.LogTrace("()");

            var batch = new List<BlockPair>();

            Task<BlockPair> dequeueTask = null;
            Task timerTask = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
            {
                // Start new dequeue task if not started already.
                dequeueTask = dequeueTask ?? this.blocksQueue.DequeueAsync();

                // Wait for one of the tasks: dequeue or timer (if available) to finish.
                Task task = (timerTask == null) ? dequeueTask : await Task.WhenAny(dequeueTask, timerTask).ConfigureAwait(false);

                bool saveBatch = false;

                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Happens when node is shutting down or Dispose() is called.
                    // We want to save whatever is in the batch before exiting the loop.
                    saveBatch = true;

                    this.logger.LogDebug("Node is shutting down. Save batch.");
                }

                // Save batch if timer ran out or we've dequeued a new block and reached the consensus tip 
                // or the max batch size is reached or the node is shutting down.  
                if (dequeueTask.Status == TaskStatus.RanToCompletion)
                {
                    BlockPair item = dequeueTask.Result;

                    // Set the dequeue task to null so it can be assigned on the next iteration.
                    dequeueTask = null;

                    batch.Add(item);

                    this.currentBatchSizeBytes += item.Block.GetSerializedSize();

                    saveBatch = saveBatch || (item.ChainedHeader == this.chain.Tip) || (this.currentBatchSizeBytes >= BatchThresholdSizeBytes);
                }
                else
                {
                    // Will be executed in case timer ran out or node is being shut down.
                    saveBatch = true;
                }

                if (saveBatch)
                {
                    if (batch.Count != 0)
                    {
                        await this.SaveBatchAsync(batch).ConfigureAwait(false);

                        batch.Clear();
                        this.currentBatchSizeBytes = 0;
                    }

                    timerTask = null;
                }
                else
                {
                    // Start timer if it is not started already.
                    timerTask = timerTask ?? Task.Delay(BatchMaxSaveIntervalSeconds * 1000, this.nodeLifetime.ApplicationStopping);
                }
            }

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>
        /// Checks if repository contains reorged blocks and deletes them; saves batch on top.
        /// The last block in the list is considered to be on the current main chain and will be used to determine if a database reorg is required.
        /// </summary>
        /// <param name="batch">List of batched blocks. Cannot be empty.</param>
        private async Task SaveBatchAsync(List<BlockPair> batch)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(batch), nameof(batch.Count), batch.Count);

            List<BlockPair> clearedBatch = this.GetBatchWithoutReorgedBlocks(batch);

            ChainedHeader expectedStoreTip = clearedBatch.First().ChainedHeader.Previous;

            // Check if block repository contains reorged blocks. If it does - delete them.
            if (expectedStoreTip.HashBlock != this.storeTip.HashBlock)
                await this.RemoveReorgedBlocksFromStoreAsync(expectedStoreTip).ConfigureAwait(false);

            // Save the batch.
            ChainedHeader newTip = clearedBatch.Last().ChainedHeader;

            this.logger.LogDebug("Saving batch of {0} blocks, total size: {1} bytes.", clearedBatch.Count, this.currentBatchSizeBytes);

            await this.blockRepository.PutAsync(newTip.HashBlock, clearedBatch.Select(b => b.Block).ToList()).ConfigureAwait(false);

            this.SetStoreTip(newTip);
            this.logger.LogDebug("Store tip set to '{0}'.", this.storeTip);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Cleans the batch in a way that all headers from the latest one are consecutive.
        /// Those that violate consecutiveness are removed.
        /// </summary>
        /// <param name="batch">Uncleaned batch that might contain non-consecutive blocks. Cannot be empty.</param>
        /// <returns>List of consecutive blocks.</returns>
        private List<BlockPair> GetBatchWithoutReorgedBlocks(List<BlockPair> batch)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(batch), nameof(batch.Count), batch.Count);

            // Initialize current with highest block from the batch.
            BlockPair current = batch.Last();

            // List of consecutive blocks. It's a cleaned out version of batch that doesn't have blocks that were reorged.
            var batchCleared = new List<BlockPair>(batch.Count) { current };
            
            // Select only those blocks that were not reorged away.
            for (int i = batch.Count - 2; i >= 0; i--)
            {
                if (batch[i].ChainedHeader.HashBlock != current.ChainedHeader.Previous.HashBlock)
                {
                    this.logger.LogDebug("Block '{0}' removed from the batch because it was reorged.", batch[i].ChainedHeader);
                    continue;
                }

                batchCleared.Add(batch[i]);
                current = batch[i];
            }

            batchCleared.Reverse();

            this.logger.LogTrace("(-):*.{0}={1}", nameof(batchCleared.Count), batchCleared.Count);
            return batchCleared;
        }

        /// <summary>Removes reorged blocks from the database.</summary>
        /// <param name="expectedStoreTip">Highest block that should be in the store.</param>
        private async Task RemoveReorgedBlocksFromStoreAsync(ChainedHeader expectedStoreTip)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(expectedStoreTip), expectedStoreTip);

            var blocksToDelete = new List<uint256>();
            ChainedHeader currentHeader = this.storeTip;

            while (currentHeader.HashBlock != expectedStoreTip.HashBlock)
            {
                blocksToDelete.Add(currentHeader.HashBlock);
                currentHeader = currentHeader.Previous;
            }

            this.logger.LogDebug("Block store reorg detected. Removing {0} blocks from the database.", blocksToDelete.Count);

            await this.blockRepository.DeleteAsync(currentHeader.HashBlock, blocksToDelete).ConfigureAwait(false);
            
            this.SetStoreTip(expectedStoreTip);
            this.logger.LogDebug("Store tip rewound to '{0}'.", this.storeTip);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            // Let current batch saving task finish.
            this.blocksQueue.Dispose();
            this.dequeueLoopTask?.GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }
    }
}