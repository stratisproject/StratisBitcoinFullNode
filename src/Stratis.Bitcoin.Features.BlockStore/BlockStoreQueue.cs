using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

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
    public class BlockStoreQueue : IBlockStoreQueue
    {
        /// <summary>Maximum interval between saving batches.</summary>
        /// <remarks>Interval value is a prime number that wasn't used as an interval in any other component. That prevents having CPU consumption spikes.</remarks>
        private const int BatchMaxSaveIntervalSeconds = 37;

        /// <summary>Maximum number of bytes the batch can hold until the downloaded blocks are stored to the disk.</summary>
        internal long BatchThresholdSizeBytes;

        /// <summary>The current batch size in bytes.</summary>
        private long currentBatchSizeBytes;

        /// <summary>The current pending blocks size in bytes.</summary>
        private long blocksQueueSizeBytes;

        /// <summary>The highest stored block in the repository.</summary>
        private ChainedHeader storeTip;

        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        private readonly IBlockStoreQueueFlushCondition blockStoreQueueFlushCondition;

        /// <inheritdoc cref="IChainState"/>
        private readonly IChainState chainState;

        /// <inheritdoc cref="StoreSettings"/>
        private readonly StoreSettings storeSettings;

        /// <inheritdoc cref="ConcurrentChain"/>
        private readonly ConcurrentChain chain;

        /// <inheritdoc cref="IBlockRepository"/>
        private readonly IBlockRepository blockRepository;

        /// <summary>Queue which contains blocks that should be saved to the database.</summary>
        private readonly AsyncQueue<ChainedHeaderBlock> blocksQueue;

        /// <summary>Batch of blocks which should be saved in the database.</summary>
        /// <remarks>Write access should be protected by <see cref="blocksCacheLock"/>.</remarks>
        private readonly List<ChainedHeaderBlock> batch;

        /// <summary>Task that runs <see cref="DequeueBlocksContinuouslyAsync"/>.</summary>
        private Task dequeueLoopTask;

        /// <summary>Protects the batch from being modifying while <see cref="GetBlockAsync"/> method is using the batch.</summary>
        private readonly object blocksCacheLock;

        /// <summary>Represents all blocks currently in the queue & pending batch, so that <see cref="GetBlockAsync"/> is able to return a value directly after enqueuing.</summary>
        /// <remarks>Write access should be protected by <see cref="blocksCacheLock"/>.</remarks>
        private readonly Dictionary<uint256, ChainedHeaderBlock> pendingBlocksCache;

        private readonly CancellationTokenSource cancellation;

        /// <inheritdoc/>
        public ChainedHeader BlockStoreCacheTip { get; private set; }

        private Exception saveAsyncLoopException;

        public BlockStoreQueue(
            ConcurrentChain chain,
            IChainState chainState,
            IBlockStoreQueueFlushCondition blockStoreQueueFlushCondition,
            StoreSettings storeSettings,
            IBlockRepository blockRepository,
            ILoggerFactory loggerFactory,
            INodeStats nodeStats)
        {
            Guard.NotNull(blockStoreQueueFlushCondition, nameof(blockStoreQueueFlushCondition));
            Guard.NotNull(chain, nameof(chain));
            Guard.NotNull(chainState, nameof(chainState));
            Guard.NotNull(storeSettings, nameof(storeSettings));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.blockStoreQueueFlushCondition = blockStoreQueueFlushCondition;
            this.chain = chain;
            this.chainState = chainState;
            this.storeSettings = storeSettings;
            this.blockRepository = blockRepository;
            this.batch = new List<ChainedHeaderBlock>();
            this.blocksCacheLock = new object();
            this.blocksQueue = new AsyncQueue<ChainedHeaderBlock>();
            this.pendingBlocksCache = new Dictionary<uint256, ChainedHeaderBlock>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.cancellation = new CancellationTokenSource();
            this.saveAsyncLoopException = null;

            this.BatchThresholdSizeBytes = storeSettings.MaxCacheSize * 1024 * 1024;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
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
            await this.blockRepository.InitializeAsync().ConfigureAwait(false);

            if (this.storeSettings.ReIndex)
            {
                await this.blockRepository.SetTxIndexAsync(this.storeSettings.TxIndex).ConfigureAwait(false);
                await this.blockRepository.ReIndexAsync().ConfigureAwait(false);
            }

            ChainedHeader initializationTip = this.chain.GetBlock(this.blockRepository.TipHashAndHeight.Hash);
            this.SetStoreTip(initializationTip);

            if (this.storeTip == null)
                await this.RecoverStoreTipAsync().ConfigureAwait(false);

            this.logger.LogDebug("Initialized block store tip at '{0}'.", this.storeTip);

            if (this.storeSettings.TxIndex != this.blockRepository.TxIndex)
            {
                if (this.storeTip != this.chain.Genesis)
                {
                    this.logger.LogTrace("(-)[REBUILD_REQUIRED]");
                    throw new BlockStoreException("You need to rebuild the block store database using -reindex to change -txindex");
                }

                // We only reach here in the case where we are syncing with a database with no blocks.
                // Always set the TxIndex here.
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

            this.BlockStoreCacheTip = initializationTip;

            // Start dequeuing.
            this.currentBatchSizeBytes = 0;
            this.dequeueLoopTask = this.DequeueBlocksContinuouslyAsync();
        }

        /// <inheritdoc/>
        public Task<Transaction> GetTransactionByIdAsync(uint256 trxid)
        {
            // Only look for transactions if they're indexed.
            if (!this.storeSettings.TxIndex)
                return Task.FromResult(default(Transaction));

            lock (this.blocksCacheLock)
            {
                foreach (ChainedHeaderBlock chainedHeaderBlock in this.pendingBlocksCache.Values)
                {
                    Transaction tx = chainedHeaderBlock.Block.Transactions.FirstOrDefault(x => x.GetHash() == trxid);

                    if (tx != null)
                    {
                        this.logger.LogTrace("Transaction '{0}' was found in the pending blocks cache.", trxid);
                        return Task.FromResult(tx);
                    }
                }
            }

            return this.blockRepository.GetTransactionByIdAsync(trxid);
        }

        /// <inheritdoc/>
        public Task<uint256> GetBlockIdByTransactionIdAsync(uint256 trxid)
        {
            lock (this.blocksCacheLock)
            {
                foreach (ChainedHeaderBlock chainedHeaderBlock in this.pendingBlocksCache.Values)
                {
                    bool exists = chainedHeaderBlock.Block.Transactions.Any(x => x.GetHash() == trxid);

                    if (exists)
                    {
                        uint256 blockId = chainedHeaderBlock.Block.GetHash();

                        this.logger.LogTrace("Block Id '{0}' with tx '{1}' was found in the pending blocks cache.", blockId, trxid);
                        return Task.FromResult(blockId);
                    }
                }
            }

            return this.blockRepository.GetBlockIdByTransactionIdAsync(trxid);
        }

        /// <inheritdoc/>
        public async Task<Block> GetBlockAsync(uint256 blockHash)
        {
            lock (this.blocksCacheLock)
            {
                if (this.pendingBlocksCache.TryGetValue(blockHash, out ChainedHeaderBlock chainedHeaderBlock))
                {
                    this.logger.LogTrace("(-)[FOUND_IN_DICTIONARY]");
                    return chainedHeaderBlock.Block;
                }
            }

            Block block = await this.blockRepository.GetBlockAsync(blockHash).ConfigureAwait(false);

            this.logger.LogTrace("Block '{0}' was{1} found in the repository.", blockHash, (block == null) ? " not" : "");

            return block;
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
            var blockStoreResetList = new List<uint256>();

            uint256 resetBlockHash = this.blockRepository.TipHashAndHeight.Hash;
            Block resetBlock = await this.blockRepository.GetBlockAsync(resetBlockHash).ConfigureAwait(false);

            while (this.chain.GetBlock(resetBlockHash) == null)
            {
                blockStoreResetList.Add(resetBlockHash);

                if (resetBlock.Header.HashPrevBlock == this.chain.Genesis.HashBlock)
                {
                    resetBlockHash = this.chain.Genesis.HashBlock;
                    break;
                }

                resetBlock = await this.blockRepository.GetBlockAsync(resetBlock.Header.HashPrevBlock).ConfigureAwait(false);

                if (resetBlock == null)
                {
                    // This can happen only if block store is corrupted.
                    throw new BlockStoreException("Block store failed to recover.");
                }

                resetBlockHash = resetBlock.GetHash();
            }

            ChainedHeader newTip = this.chain.GetBlock(resetBlockHash);

            if (blockStoreResetList.Count != 0)
                await this.blockRepository.DeleteAsync(new HashHeightPair(newTip), blockStoreResetList).ConfigureAwait(false);

            this.SetStoreTip(newTip);

            //TODO this thing should remove stuff from chain database. Otherwise we are leaving redundant data.
            this.chain.SetTip(newTip); // we have to set chain store to be same as the store tip.

            this.logger.LogWarning("Block store tip recovered to block '{0}'.", newTip);
        }

        private void AddComponentStats(StringBuilder log)
        {
            if (this.storeTip != null)
            {
                log.AppendLine();
                log.AppendLine("======BlockStore======");
                log.AppendLine($"Batch Size: {this.currentBatchSizeBytes.BytesToMegaBytes()} MB / {this.BatchThresholdSizeBytes.BytesToMegaBytes()} MB ({this.batch.Count} batched blocks)");
                log.AppendLine($"Queue Size: {this.blocksQueueSizeBytes.BytesToMegaBytes()} MB ({this.blocksQueue.Count} queued blocks)");
            }
        }

        /// <inheritdoc />
        public void AddToPending(ChainedHeaderBlock chainedHeaderBlock)
        {
            // Throw any error encountered by the asynchronous loop.
            if (this.saveAsyncLoopException != null)
                throw this.saveAsyncLoopException;

            lock (this.blocksCacheLock)
            {
                if (this.pendingBlocksCache.TryAdd(chainedHeaderBlock.ChainedHeader.HashBlock, chainedHeaderBlock))
                {
                    this.logger.LogTrace("Block '{0}' was added to pending.", chainedHeaderBlock.ChainedHeader);
                }
                else
                {
                    // If the chained header block already exists, we need to remove it and add to the back of the collection.
                    this.pendingBlocksCache.Remove(chainedHeaderBlock.ChainedHeader.HashBlock);
                    this.pendingBlocksCache.Add(chainedHeaderBlock.ChainedHeader.HashBlock, chainedHeaderBlock);
                    this.logger.LogTrace("Block '{0}' was re-added to pending.", chainedHeaderBlock.ChainedHeader);
                }

                this.BlockStoreCacheTip = chainedHeaderBlock.ChainedHeader;
            }

            this.blocksQueue.Enqueue(chainedHeaderBlock);
            this.blocksQueueSizeBytes += chainedHeaderBlock.Block.BlockSize.Value;
        }

        /// <summary>
        /// Dequeues the blocks continuously and saves them to the database when max batch size is reached or timer ran out.
        /// </summary>
        /// <remarks>Batch is always saved on shutdown.</remarks>
        private async Task DequeueBlocksContinuouslyAsync()
        {
            Task<ChainedHeaderBlock> dequeueTask = null;
            Task timerTask = null;

            while (!this.cancellation.IsCancellationRequested)
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
                    // Can happen if Dispose() was called.
                    // We want to save whatever is in the batch before exiting the loop.
                    saveBatch = true;

                    this.logger.LogDebug("Node is shutting down. Save batch.");
                }

                // Save batch if timer ran out or we've dequeued a new block and reached the consensus tip
                // or the max batch size is reached or the node is shutting down.
                if (dequeueTask.Status == TaskStatus.RanToCompletion)
                {
                    ChainedHeaderBlock item = dequeueTask.Result;

                    // Set the dequeue task to null so it can be assigned on the next iteration.
                    dequeueTask = null;

                    lock (this.blocksCacheLock)
                    {
                        this.batch.Add(item);
                    }

                    this.blocksQueueSizeBytes -= item.Block.BlockSize.Value;
                    this.currentBatchSizeBytes += item.Block.BlockSize.Value;

                    saveBatch = saveBatch || (this.currentBatchSizeBytes >= this.BatchThresholdSizeBytes) || this.blockStoreQueueFlushCondition.ShouldFlush;
                }
                else
                {
                    // Will be executed in case timer ran out or node is being shut down.
                    saveBatch = true;
                }

                if (saveBatch)
                {
                    if (this.batch.Count != 0)
                    {
                        try
                        {
                            await this.SaveBatchAsync().ConfigureAwait(false);

                            // If an error occurred during SaveBatchAsync then this code
                            // which clears the batch will not execute.
                            lock (this.blocksCacheLock)
                            {
                                foreach (ChainedHeaderBlock chainedHeaderBlock in this.batch)
                                {
                                    this.pendingBlocksCache.Remove(chainedHeaderBlock.ChainedHeader.HashBlock);
                                }

                                this.batch.Clear();
                            }

                            this.currentBatchSizeBytes = 0;
                        }
                        catch (Exception err)
                        {
                            this.logger.LogError("Could not save blocks to the block repository. Exiting due to '{0}'.", err.Message);
                            this.saveAsyncLoopException = err;
                            throw;
                        }
                    }

                    timerTask = null;
                }
                else
                {
                    // Start timer if it is not started already.
                    timerTask = timerTask ?? Task.Delay(BatchMaxSaveIntervalSeconds * 1000, this.cancellation.Token);
                }
            }

            await this.FlushAllCollectionsAsync();
        }

        /// <summary>
        /// Ensures that any blocks queued in <see cref="blocksQueue"/> gets added to <see cref="batch"/>
        /// so that it can be persisted on dispose.
        /// </summary>
        private async Task FlushAllCollectionsAsync()
        {
            ChainedHeaderBlock chainedHeaderBlock = null;
            while (this.blocksQueue.TryDequeue(out chainedHeaderBlock))
            {
                this.batch.Add(chainedHeaderBlock);
            }

            if (this.batch.Count != 0)
                await this.SaveBatchAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if repository contains reorged blocks and deletes them; saves batch on top.
        /// The last block in the list is considered to be on the current main chain and will be used to determine if a database reorg is required.
        /// </summary>
        /// <exception cref="DBreeze.Exceptions.DBreezeException">Thrown if an error occurs during database operations.</exception>
        private async Task SaveBatchAsync()
        {
            List<ChainedHeaderBlock> clearedBatch = this.GetBatchWithoutReorgedBlocks();

            ChainedHeader expectedStoreTip = clearedBatch.First().ChainedHeader.Previous;

            // Check if block repository contains reorged blocks. If it does - delete them.
            if (expectedStoreTip.HashBlock != this.storeTip.HashBlock)
                await this.RemoveReorgedBlocksFromStoreAsync(expectedStoreTip).ConfigureAwait(false);

            // Save the batch.
            ChainedHeader newTip = clearedBatch.Last().ChainedHeader;

            this.logger.LogDebug("Saving batch of {0} blocks, total size: {1} bytes.", clearedBatch.Count, this.currentBatchSizeBytes);

            await this.blockRepository.PutAsync(new HashHeightPair(newTip), clearedBatch.Select(b => b.Block).ToList()).ConfigureAwait(false);

            this.SetStoreTip(newTip);
            this.logger.LogDebug("Store tip set to '{0}'.", this.storeTip);
        }

        /// <summary>
        /// Cleans the batch in a way that all headers from the latest one are consecutive.
        /// Those that violate consecutiveness are removed.
        /// </summary>
        /// <returns>List of consecutive blocks.</returns>
        private List<ChainedHeaderBlock> GetBatchWithoutReorgedBlocks()
        {
            // Initialize current with highest block from the batch.
            ChainedHeaderBlock current = this.batch.Last();

            // List of consecutive blocks. It's a cleaned out version of batch that doesn't have blocks that were reorged.
            var batchCleared = new List<ChainedHeaderBlock>(this.batch.Count) { current };

            // Select only those blocks that were not reorged away.
            for (int i = this.batch.Count - 2; i >= 0; i--)
            {
                if (this.batch[i].ChainedHeader.HashBlock != current.ChainedHeader.Previous.HashBlock)
                {
                    this.logger.LogDebug("Block '{0}' removed from the batch because it was reorged.", this.batch[i].ChainedHeader);
                    continue;
                }

                batchCleared.Add(this.batch[i]);
                current = this.batch[i];
            }

            batchCleared.Reverse();

            return batchCleared;
        }

        /// <summary>Removes reorged blocks from the database.</summary>
        /// <param name="expectedStoreTip">Highest block that should be in the store.</param>
        /// <exception cref="DBreeze.Exceptions.DBreezeException">Thrown if an error occurs during database operations.</exception>
        private async Task RemoveReorgedBlocksFromStoreAsync(ChainedHeader expectedStoreTip)
        {
            var blocksToDelete = new List<uint256>();
            ChainedHeader currentHeader = this.storeTip;

            while (currentHeader.HashBlock != expectedStoreTip.HashBlock)
            {
                blocksToDelete.Add(currentHeader.HashBlock);

                if (currentHeader.Previous == null)
                    break;

                currentHeader = currentHeader.Previous;
            }

            this.logger.LogDebug("Block store reorg detected. Removing {0} blocks from the database.", blocksToDelete.Count);

            await this.blockRepository.DeleteAsync(new HashHeightPair(currentHeader), blocksToDelete).ConfigureAwait(false);

            this.SetStoreTip(expectedStoreTip);
            this.logger.LogDebug("Store tip rewound to '{0}'.", this.storeTip);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Let current batch saving task finish.
            this.cancellation.Cancel();
            this.blocksQueue.Dispose();
            this.dequeueLoopTask?.GetAwaiter().GetResult();
            this.blockRepository.Dispose();
        }
    }
}