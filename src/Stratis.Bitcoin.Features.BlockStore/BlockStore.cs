using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Interfaces;
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

        private readonly ILogger logger;
        private readonly INodeLifetime nodeLifetime;
        private readonly IChainState chainState;

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
            this.blocksQueue = new AsyncQueue<BlockPair>();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.currentBatchSizeBytes = 0;

            this.dequeueLoopTask = this.DequeueBlocksContinuouslyAsync();
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
                    if (batch.Count == 0)
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
            



            //TODO del if reorg, save normally otherwise
        }

        public void Dispose()
        {
            // Let current batch saving task finish.
            this.blocksQueue.Dispose();
            this.dequeueLoopTask.GetAwaiter().GetResult();
        }
    }
}


//TODO remove store block puller, block store loop, loopsteps

//when we initialize block store- load block store tip. if it's below consensus tip- rewind consensus