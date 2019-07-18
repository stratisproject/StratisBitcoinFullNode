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
    /// Queue which contains blocks that are processed by a callback after enqueueing. Limited by a maximum size in bytes. If this size is reached,
    /// enqueuing new blocks will be unsuccessful.
    /// </summary>
    public class BlockQueueProcessor : IDisposable
    {
        /// <summary>Queue which contains blocks that should be processed by <see cref="WalletManager"/>.</summary>
        private readonly IAsyncDelegateDequeuer<Block> blocksQueue;

        /// <summary>Current <see cref="blocksQueue"/> size in bytes.</summary>
        private long blocksQueueSize;

        /// <summary>Flag to determine when the <see cref="MaxQueueSize"/> is reached.</summary>
        private bool maxQueueSizeReached;

        private readonly ILogger logger;

        private readonly Func<Block, CancellationToken, Task> callback;

        public BlockQueueProcessor(ILogger logger, IAsyncProvider asyncProvider, Func<Block, CancellationToken, Task> callback, int maxQueueSize = 100 * 1024 * 1024, string friendlyName = null)
        {
            this.logger = logger;
            this.MaxQueueSize = maxQueueSize;
            this.callback = callback;

            if (friendlyName == null)
            {
                friendlyName = nameof(BlockQueueProcessor);
            }

            this.blocksQueue = asyncProvider.CreateAndRunAsyncDelegateDequeuer<Block>($"{friendlyName}-{nameof(this.blocksQueue)}", this.OnProcessBlockAsync);
        }

        /// <summary>Limits the <see cref="blocksQueue"/> size, in bytes.</summary>
        public int MaxQueueSize { get; }

        public long QueueSizeBytes => this.blocksQueueSize;

        private async Task OnProcessBlockAsync(Block block, CancellationToken cancellationToken)
        {
            long currentBlockQueueSize = Interlocked.Add(ref this.blocksQueueSize, -block.BlockSize.Value);

            this.logger.LogDebug("Block '{0}' queued for processing. Queue size changed to {1} bytes.", block.GetHash(), currentBlockQueueSize);

            await this.callback(block, cancellationToken);
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
                if (this.blocksQueueSize >= this.MaxQueueSize)
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
                this.logger.LogDebug("Queue sized changed to {0} bytes.", currentBlockQueueSize);

                this.blocksQueue.Enqueue(block);

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
