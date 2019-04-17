using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Features.FederatedPeg.Wallet
{
    /// <summary>
    /// Queue which contains blocks for processing by a callback. Limited by a maximum size. If this size is reached,
    /// enqueuing new blocks will be unsuccessful.
    /// </summary>
    public class BlockQueue : IDisposable
    {
        /// <summary>Queue which contains blocks that should be processed by <see cref="WalletManager"/>.</summary>
        private readonly IAsyncDelegateDequeuer<Block> blocksQueue;

        /// <summary>Current <see cref="blocksQueue"/> size in bytes.</summary>
        private long blocksQueueSize;

        /// <summary>Flag to determine when the <see cref="MaxQueueSize"/> is reached.</summary>
        private bool maxQueueSizeReached;

        private readonly ILogger logger;

        /// <summary>Limit <see cref="blocksQueue"/> size to 100MB.</summary>
        private readonly int maxQueueSize;

        private readonly Func<Block, CancellationToken, Task> callback;

        public BlockQueue(ILogger logger, IAsyncProvider asyncProvider, Func<Block, CancellationToken, Task> callback, int maxQueueSize = 100 * 1024 * 1024)
        {
            this.logger = logger;
            this.maxQueueSize = maxQueueSize;
            this.callback = callback;
            this.blocksQueue = asyncProvider.CreateAndRunAsyncDelegateDequeuer<Block>($"{nameof(FederationWalletSyncManager)}-{nameof(this.blocksQueue)}", this.OnProcessBlockAsync);
        }

        private async Task OnProcessBlockAsync(Block block, CancellationToken cancellationToken)
        {
            long currentBlockQueueSize = Interlocked.Add(ref this.blocksQueueSize, -block.BlockSize.Value);
            
            this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

            await this.callback(block, cancellationToken);
        }

        private void Enqueue(Block block)
        {
            long currentBlockQueueSize = Interlocked.Add(ref this.blocksQueueSize, block.BlockSize.Value);
            this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

            this.blocksQueue.Enqueue(block);
        }

        /// <summary>
        /// Attempts to enqueue the block. If the maximum queue size has been reached, this will return false, and the block will
        /// not be queued.
        /// </summary>
        /// <param name="block">The block to enqueue.</param>
        /// <returns>True, if enqueuing the block was successful.</returns>
        public bool TryEnqueue(Block block)
        {
            Guard.NotNull(block, nameof(block));

            // If the queue reaches the maximum limit, ignore incoming blocks until the queue is empty.
            if (!this.maxQueueSizeReached)
            {
                if (this.blocksQueueSize >= this.maxQueueSize)
                {
                    this.maxQueueSizeReached = true;
                    this.logger.LogTrace("(-)[REACHED_MAX_QUEUE_SIZE]");
                }
            }
            else
            {
                // If queue is empty then reset the maxQueueSizeReached flag.
                this.maxQueueSizeReached = this.blocksQueueSize > 0;
            }

            if (!this.maxQueueSizeReached)
            {
                long currentBlockQueueSize = Interlocked.Add(ref this.blocksQueueSize, block.BlockSize.Value);
                this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

                this.Enqueue(block);

                return true;
            }

            return false;
        }

        public void Dispose()
        {
            this.blocksQueue.Dispose();
        }
    }
}