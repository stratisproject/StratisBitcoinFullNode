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
    // run tests
    // todo write tests

    /// <summary>
    /// Saves blocks to the database in batches, removes reorged blocks from the database.
    /// </summary>
    public class BlockStore : IDisposable
    {
        private const int BatchMaxSaveIntervalSeconds = 37;

        /// <summary>Maximum number of bytes the batch can hold until the downloaded blocks are stored to the disk.</summary>
        private const uint BatchThresholdSizeBytes = 5 * 1000 * 1000;

        private int currentBatchSizeBytes;

        /// <summary>The highest stored block in the repository.</summary>
        public ChainedHeader StoreTip { get; private set; }

        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;
        private readonly IChainState chainState;
        private readonly StoreSettings storeSettings;
        private readonly ConcurrentChain chain;
        private readonly IBlockRepository blockRepository;

        private readonly AsyncQueue<BlockPair> blocksQueue;

        /// <summary>Task that runs <see cref="DequeueBlocksContinuouslyAsync"/>.</summary>
        private Task dequeueLoopTask;

        public BlockStore(
            IBlockRepository blockRepository,
            ConcurrentChain chain,
            IChainState chainState,
            StoreSettings storeSettings,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory)
        {
            this.chainState = chainState;
            this.nodeLifetime = nodeLifetime;
            this.storeSettings = storeSettings;
            this.chain = chain;
            this.blockRepository = blockRepository;

            this.blocksQueue = new AsyncQueue<BlockPair>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        private void SetStoreTip(ChainedHeader newTip)
        {
            this.StoreTip = newTip;
            this.chainState.BlockStoreTip = newTip;
        }

        /// <summary>
        /// Initializes the <see cref="BlockStore"/>.
        /// <para>
        /// If StoreTip is <c>null</c>, the store is out of sync. This can happen when:</para>
        /// <list>
        ///     <item>1. The node crashed.</item>
        ///     <item>2. The node was not closed down properly.</item>
        /// </list>
        /// <para>
        /// To recover we walk back the chain until a common block header is found and set the BlockStore's StoreTip to that.
        /// </para>
        /// </summary>
        public async Task InitializeAsync()
        {
            this.logger.LogTrace("()");

            if (this.storeSettings.ReIndex)
                throw new NotImplementedException();

            this.SetStoreTip(this.chain.GetBlock(this.blockRepository.BlockHash));
            
            if (this.StoreTip == null)
                await this.RecoverStoreTipAsync().ConfigureAwait(false);

            this.logger.LogDebug("Initialized block store tip at '{0}'.", this.StoreTip);

            if (this.storeSettings.TxIndex != this.blockRepository.TxIndex)
            {
                if (this.StoreTip != this.chain.Genesis)
                    throw new BlockStoreException("You need to rebuild the block store database using -reindex-chainstate to change -txindex");

                if (this.storeSettings.TxIndex)
                    await this.blockRepository.SetTxIndexAsync(this.storeSettings.TxIndex).ConfigureAwait(false);
            }
            
            // Make sure that block store initializes before the consensus. 
            // This is needed in order to rewind consensus in case it is initialized ahead of the block store.
            if (this.chainState.ConsensusTip != null)
            {
                this.logger.LogCritical("Block store initialized after the consensus!");
                throw new Exception("Block store initialized after consensus!");
            }

            // Start dequeuing.
            this.currentBatchSizeBytes = 0;
            this.dequeueLoopTask = this.DequeueBlocksContinuouslyAsync();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sets block store tip to the last block that exists both in the repository and in the <see cref="ConcurrentChain"/>.
        /// </summary>
        private async Task RecoverStoreTipAsync()
        {
            this.logger.LogTrace("()");

            var blockStoreResetList = new List<uint256>();
            Block resetBlock = await this.blockRepository.GetAsync(this.blockRepository.BlockHash).ConfigureAwait(false);
            uint256 resetBlockHash = resetBlock.GetHash();

            while (this.chain.GetBlock(resetBlockHash) == null)
            {
                blockStoreResetList.Add(resetBlockHash);

                if (resetBlock.Header.HashPrevBlock == this.chain.Genesis.HashBlock)
                {
                    resetBlockHash = this.chain.Genesis.HashBlock;
                    break;
                }

                resetBlock = await this.blockRepository.GetAsync(resetBlock.Header.HashPrevBlock).ConfigureAwait(false);
                Guard.NotNull(resetBlock, nameof(resetBlock));
                resetBlockHash = resetBlock.GetHash();
            }

            ChainedHeader newTip = this.chain.GetBlock(resetBlockHash);
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


            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested || batch.Count != 0)
            {
                // Start new dequeue task if not started already.
                dequeueTask = dequeueTask ?? this.blocksQueue.DequeueAsync();

                // Wait for one of the tasks: dequeue or timer (if available) to finish.
                Task task = timerTask == null ? dequeueTask : await Task.WhenAny(dequeueTask, timerTask).ConfigureAwait(false);

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

                // Save batch if timer ran out or we've dequeued a new block and reached the consensus tip or the max batch size is reached.  
                if (dequeueTask.Status == TaskStatus.RanToCompletion)
                {
                    BlockPair item = dequeueTask.Result;

                    // Set the dequeue task to null so it can be assigned on the next iteration.
                    dequeueTask = null;

                    batch.Add(item);

                    this.currentBatchSizeBytes += item.Block.GetSerializedSize();

                    if (saveBatch == false)
                        saveBatch = (item.ChainedHeader == this.chain.Tip) || (this.currentBatchSizeBytes >= BatchThresholdSizeBytes);
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

        /// <summary>Checks if repository contains reorged blocks and deletes them; saves batch on top.</summary>
        private async Task SaveBatchAsync(List<BlockPair> batch)
        {
            this.logger.LogTrace("()");

            List<BlockPair> batchCleared = this.GetBatchWithoutReorgedBlocks(batch);

            ChainedHeader expectedStoreTip = batchCleared.First().ChainedHeader.Previous;

            // Check if block repository contains reorged blocks. If it does - delete them.
            if (expectedStoreTip.HashBlock != this.StoreTip.HashBlock)
            {
                var blocksToDelete = new List<uint256>();
                ChainedHeader currentHeader = this.StoreTip;

                while (currentHeader.HashBlock != expectedStoreTip.HashBlock)
                {
                    blocksToDelete.Add(currentHeader.HashBlock);
                    currentHeader = currentHeader.Previous;
                }

                this.logger.LogDebug("Block store reorg detected. Removing {0} blocks from the database.", blocksToDelete.Count);

                await this.blockRepository.DeleteAsync(currentHeader.HashBlock, blocksToDelete).ConfigureAwait(false);

                this.logger.LogDebug("Store tip rewinded to '{0}'", this.StoreTip);
                this.SetStoreTip(expectedStoreTip);
            }

            // Save batch.
            ChainedHeader newTip = batchCleared.Last().ChainedHeader;

            this.logger.LogDebug("Saving batch of {0} blocks, total size: {1} bytes.", batchCleared.Count, this.currentBatchSizeBytes);

            await this.blockRepository.PutAsync(newTip.HashBlock, batchCleared.Select(b => b.Block).ToList()).ConfigureAwait(false);

            this.SetStoreTip(newTip);

            this.logger.LogDebug("Store tip set to '{0}'", this.StoreTip);
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Cleans the batch in a way that all headers from the latest one are consecutive. 
        /// Those that violate consecutiveness are removed.
        /// </summary>
        private List<BlockPair> GetBatchWithoutReorgedBlocks(List<BlockPair> batch)
        {
            this.logger.LogTrace("()");

            // Initialize current with highest block from the batch (it's consensus tip).
            BlockPair current = batch.Last();

            // List of consecutive blocks. It's a cleaned out version of batch that doesn't have blocks that were reorged.
            var batchCleared = new List<BlockPair>();

            batchCleared.Add(current);

            // Select only those blocks that were not reorged away.
            for (int i = batch.Count - 2; i >= 0; i--)
            {
                if (batch[i].ChainedHeader.HashBlock != current.ChainedHeader.Previous.HashBlock)
                {
                    this.logger.LogDebug("Block '{0}' removed from the batch because it was reorged.", batch[i].ChainedHeader);
                    continue;
                }

                batchCleared.Insert(0, batch[i]);
                current = batch[i];
            }

            this.logger.LogTrace("(-)");
            return batchCleared;
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