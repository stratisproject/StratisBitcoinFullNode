using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of <see cref="AsyncQueue{T}"/> class.
    /// </summary>
    public class AsyncQueueTest
    {
        /// <summary>Source of randomness.</summary>
        private Random random = new Random();

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.Dispose"/> triggers cancellation inside the on-enqueue callback.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DisposeCancelsEnqueueAsync()
        {
            bool signal = false;

            var asyncQueue = new AsyncQueue<int>(async (item, cancellation) =>
            {
                // We set the signal and wait and if the wait is finished, we reset the signal, but that should not happen.
                signal = true;
                await Task.Delay(500, cancellation);
                signal = false;
            });

            // Enqueue an item, which should trigger the callback.
            asyncQueue.Enqueue(1);

            // Wait a bit and dispose the queue, which should trigger the cancellation.
            await Task.Delay(100);
            asyncQueue.Dispose();

            Assert.True(signal);
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.Dispose"/> triggers waits until the on-enqueue callback (and the consumer task) 
        /// are finished before returning to the caller.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DisposeCancelsAndWaitsEnqueueAsync()
        {
            bool signal = true;

            var asyncQueue = new AsyncQueue<int>(async (item, cancellation) =>
            {
                // We only set the signal if the wait is finished.
                await Task.Delay(250);
                signal = false;
            });

            // Enqueue an item, which should trigger the callback.
            asyncQueue.Enqueue(1);

            // Wait a bit and dispose the queue, which should trigger the cancellation.
            await Task.Delay(100);
            asyncQueue.Dispose();

            Assert.False(signal);
        }

        /// <summary>
        /// Tests the guarantee of <see cref="AsyncQueue{T}"/> that only one instance of the callback is executed at the moment
        /// regardless of how many enqueue operations occur.
        /// </summary>
        [Fact]
        public async void AsyncQueue_OnlyOneInstanceOfCallbackExecutesAsync()
        {
            bool executingCallback = false;

            int itemsToProcess = 20;
            int itemsProcessed = 0;
            ManualResetEventSlim allItemsProcessed = new ManualResetEventSlim();

            var asyncQueue = new AsyncQueue<int>(async (item, cancellation) =>
            {
                // Mark the callback as executing and wait a bit to make sure other callback operations can happen in the meantime.
                Assert.False(executingCallback);

                executingCallback = true;
                await Task.Delay(this.random.Next(100));

                itemsProcessed++;

                if (itemsProcessed == itemsToProcess) allItemsProcessed.Set();

                executingCallback = false;
            });

            // Adds items quickly so that next item is likely to be enqueued before the previous callback finishes.
            // We make small delays between enqueue operations, to make sure not all items are processed in one batch.
            for (int i = 0; i < itemsToProcess; i++)
            {
                asyncQueue.Enqueue(i);
                await Task.Delay(this.random.Next(10));
            }

            // Wait for all items to be processed.
            allItemsProcessed.Wait();

            Assert.Equal(itemsToProcess, itemsProcessed);

            allItemsProcessed.Dispose();

            asyncQueue.Dispose();
        }

        /// <summary>
        /// Tests that the order of enqueue operations is preserved in callbacks.
        /// </summary>
        [Fact]
        public void AsyncQueue_EnqueueOrderPreservedInCallbacks()
        {
            int itemsToProcess = 30;
            int itemPrevious = -1;
            ManualResetEventSlim signal = new ManualResetEventSlim();

            var asyncQueue = new AsyncQueue<int>(async (item, cancellation) =>
            {
                // Wait a bit to make sure other enqueue operations can happen in the meantime.
                await Task.Delay(this.random.Next(50));
                Assert.Equal(itemPrevious + 1, item);
                itemPrevious = item;

                if (item + 1 == itemsToProcess) signal.Set();
            });

            // Enqueue items quickly, so that next item is likely to be enqueued before the previous callback finishes.
            for (int i = 0; i < itemsToProcess; i++)
                asyncQueue.Enqueue(i);

            // Wait for all items to be processed.
            signal.Wait();
            signal.Dispose();

            asyncQueue.Dispose();
        }

        /// <summary>
        /// Tests that if the queue is disposed, not all items are necessarily processed.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DisposeCanDiscardItemsAsync()
        {
            int itemsToProcess = 100;
            int itemsProcessed = 0;

            var asyncQueue = new AsyncQueue<int>(async (item, cancellation) =>
            {
                // Wait a bit to make sure other enqueue operations can happen in the meantime.
                await Task.Delay(this.random.Next(30));
                itemsProcessed++;
            });

            // Enqueue items quickly, so that next item is likely to be enqueued before the previous callback finishes.
            for (int i = 0; i < itemsToProcess; i++)
                asyncQueue.Enqueue(i);

            // Wait a bit, but not long enough to process all items.
            await Task.Delay(200);

            asyncQueue.Dispose();
            Assert.True(itemsProcessed < itemsToProcess);
        }
    }
}
