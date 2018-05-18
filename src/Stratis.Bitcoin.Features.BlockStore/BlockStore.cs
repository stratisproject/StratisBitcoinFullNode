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
    /// TODO
    /// </summary>
    public class BlockStore : IDisposable
    {
        private const int BatchMaxSaveIntervalSeconds = 37;

        /// <summary>Maximum number of bytes the batch can hold until the downloaded blocks are stored to the disk.</summary>
        private const uint BatchThresholdSizeBytes = 5 * 1000 * 1000;

        private int currentBatchSizeBytes;

        /// <summary>The highest stored block in the repository.</summary>
        private ChainedHeader storeTip;

        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;
        private readonly IChainState chainState;
        private readonly StoreSettings storeSettings;
        private readonly ConcurrentChain chain;
        private readonly IBlockRepository blockRepository;

        private readonly AsyncQueue<BlockPair> blocksQueue;

        /// <summary>Task that runs <see cref="DequeueBlocksContinuouslyAsync"/>.</summary>
        private readonly Task dequeueLoopTask;

        public BlockStore(
            IBlockRepository blockRepository,
            IBlockStoreCache cache,
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

            this.currentBatchSizeBytes = 0;

            this.dequeueLoopTask = this.DequeueBlocksContinuouslyAsync();
        }
        
        /// <summary> TODO review comment
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

            this.storeTip = this.chain.GetBlock(this.blockRepository.BlockHash);

            if (this.storeTip == null)
                await this.RecoverStoreTipAsync().ConfigureAwait(false);

            if (this.storeSettings.TxIndex != this.blockRepository.TxIndex)
            {
                if (this.storeTip != this.chain.Genesis)
                    throw new BlockStoreException("You need to rebuild the block store database using -reindex-chainstate to change -txindex");

                if (this.storeSettings.TxIndex)
                    await this.blockRepository.SetTxIndexAsync(this.storeSettings.TxIndex).ConfigureAwait(false);
            }

            this.SetHighestPersistedBlock(this.storeTip);

            this.logger.LogTrace("(-)");
        }

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
            this.storeTip = newTip;
            this.logger.LogWarning("Block store tip recovered to block '{0}'.", newTip);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Set the highest persisted block in the chain.</summary>
        private void SetHighestPersistedBlock(ChainedHeader block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(block), block?.HashBlock);

            if (this.blockRepository is BlockRepository blockRepository)
                blockRepository.HighestPersistedBlock = block;

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

        private async Task DequeueBlocksContinuouslyAsync()
        {
            var batch = new List<BlockPair>();

            Task<BlockPair> dequeueTask = null;
            Task timerTask = null;

            while (!this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
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
                        saveBatch = (item.ChainedHeader == this.chainState.ConsensusTip) || (this.currentBatchSizeBytes >= BatchThresholdSizeBytes);
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
        }

        private async Task SaveBatchAsync(List<BlockPair> batch)
        {
            List<BlockPair> batchCleared = this.GetBatchWithoutReorgedBlocks(batch);

            ChainedHeader expectedStoreTip = batchCleared.First().ChainedHeader.Previous;

            // Check if block repository contains reorged blocks. If it does - delete them.
            if (expectedStoreTip.HashBlock != this.storeTip.HashBlock)
            {
                var blocksToDelete = new List<uint256>();
                ChainedHeader currentHeader = this.storeTip;

                while (currentHeader.HashBlock != expectedStoreTip.HashBlock)
                {
                    blocksToDelete.Add(currentHeader.HashBlock);
                    currentHeader = currentHeader.Previous;
                }

                await this.blockRepository.DeleteAsync(currentHeader.HashBlock, blocksToDelete).ConfigureAwait(false);

                this.storeTip = expectedStoreTip;
            }

            // Save batch.
            ChainedHeader newTip = batchCleared.Last().ChainedHeader;

            await this.blockRepository.PutAsync(newTip.HashBlock, batchCleared.Select(b => b.Block).ToList()).ConfigureAwait(false);
            
            this.SetHighestPersistedBlock(this.storeTip);
            this.storeTip = newTip;
        }

        private List<BlockPair> GetBatchWithoutReorgedBlocks(List<BlockPair> batch)
        {
            // Initialize current with highest block from the batch (it's consensus tip).
            BlockPair current = batch.Last();

            // List of consecutive blocks. It's a cleaned out version of batch that doesn't have blocks that were reorged.
            var batchCleared = new List<BlockPair>();

            batchCleared.Add(current);

            // Select only those blocks that were not reorged away.
            for (int i = batch.Count - 1; i >= 0; i--)
            {
                if (batch[i].ChainedHeader.HashBlock != current.ChainedHeader.Previous.HashBlock)
                    continue;

                batchCleared.Insert(0, batch[i]);
            }

            return batchCleared;
        }

        public void Dispose()
        {
            // Let current batch saving task finish.
            this.blocksQueue.Dispose();
            this.dequeueLoopTask.GetAwaiter().GetResult();
        }
    }
}

//when we initialize block store- load block store tip. if it's below consensus tip- rewind consensus
//todo tests
//TODO comment this class properly