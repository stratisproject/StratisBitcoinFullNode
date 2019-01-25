using System;
using System.Collections.Generic;
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
        /// Tests that <see cref="AsyncQueue{T}.Dispose"/> waits until the on-enqueue callback (and the consumer task) 
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
            var allItemsProcessed = new ManualResetEventSlim();

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
            var signal = new ManualResetEventSlim();

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


        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.DequeueAsync(CancellationToken)"/> throws cancellation exception 
        /// when the passed cancellation token is cancelled.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DequeueCancellationAsync()
        {
            int itemsToProcess = 50;
            int itemsProcessed = 0;

            // Create a queue in blocking dequeue mode.
            var asyncQueue = new AsyncQueue<int>();

            Task consumer = Task.Run(async () =>
            {
                using (var cts = new CancellationTokenSource(250))
                {
                    while (true)
                    {
                        try
                        {
                            int item = await asyncQueue.DequeueAsync(cts.Token);

                            await Task.Delay(this.random.Next(10));
                            itemsProcessed++;
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
            });

            for (int i = 0; i < itemsToProcess; i++)
            {
                asyncQueue.Enqueue(i);
                await Task.Delay(this.random.Next(10) + 10);
            }

            // Check that the consumer task ended already.
            Assert.True(consumer.IsCompleted);

            asyncQueue.Dispose();

            // Check that not all items were processed.
            Assert.True(itemsProcessed < itemsToProcess);
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.DequeueAsync(CancellationToken)"/> provides items in correct order 
        /// and that it throws cancellation exception when the queue is disposed.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DequeueAndDisposeAsync()
        {
            int itemsToProcess = 50;

            // Create a queue in blocking dequeue mode.
            var asyncQueue = new AsyncQueue<int>();

            // List of items collected by the consumer task.
            var list = new List<int>();

            Task consumer = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        int item = await asyncQueue.DequeueAsync();

                        await Task.Delay(this.random.Next(10) + 1);

                        list.Add(item);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });

            // Add half of the items slowly, so that the consumer is able to empty the queue.
            // Add the rest of the items very quickly, so that the consumer won't be able to process all of them.
            for (int i = 0; i < itemsToProcess; i++)
            {
                asyncQueue.Enqueue(i);

                if (i < itemsToProcess / 2)
                    await Task.Delay(this.random.Next(10) + 5);
            }

            // Give the consumer little more time to process couple more items.
            await Task.Delay(20);

            // Dispose the queue, which should cause the first consumer task to terminate.
            asyncQueue.Dispose();

            await consumer;

            // Check that the list contains items in correct order.
            for (int i = 0; i < list.Count - 1; i++)
                Assert.Equal(list[i] + 1, list[i + 1]);

            // Check that not all items were processed.
            Assert.True(list.Count < itemsToProcess);
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.DequeueAsync(CancellationToken)"/> throws cancellation exception 
        /// if it is called after the queue was disposed.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DequeueThrowsAfterDisposeAsync()
        {
            // Create a queue in blocking dequeue mode.
            var asyncQueue = new AsyncQueue<int>();

            asyncQueue.Enqueue(1);

            asyncQueue.Dispose();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await asyncQueue.DequeueAsync());
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.DequeueAsync(CancellationToken)"/> blocks when the queue is empty.
        /// </summary>
        [Fact]
        public void AsyncQueue_DequeueBlocksOnEmptyQueue()
        {
            // Create a queue in blocking dequeue mode.
            var asyncQueue = new AsyncQueue<int>();

            Assert.False(asyncQueue.DequeueAsync().Wait(100));
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.DequeueAsync(CancellationToken)"/> can be used by 
        /// two different threads safely.
        /// </summary>
        [Fact]
        public async void AsyncQueue_DequeueParallelAsync()
        {
            int itemsToProcess = 50;

            // Create a queue in blocking dequeue mode.
            var asyncQueue = new AsyncQueue<int>();

            // List of items collected by the consumer tasks.
            var list1 = new List<int>();
            var list2 = new List<int>();

            using (var cts = new CancellationTokenSource())
            {
                // We create two consumer tasks that compete for getting items from the queue.
                Task consumer1 = Task.Run(async () => await this.AsyncQueue_DequeueParallelAsync_WorkerAsync(asyncQueue, list1, itemsToProcess - 1, cts));
                Task consumer2 = Task.Run(async () => await this.AsyncQueue_DequeueParallelAsync_WorkerAsync(asyncQueue, list2, itemsToProcess - 1, cts));

                // Start adding the items.
                for (int i = 0; i < itemsToProcess; i++)
                {
                    asyncQueue.Enqueue(i);
                    await Task.Delay(this.random.Next(10));
                }

                // Wait until both consumers are finished.
                Task.WaitAll(consumer1, consumer2);
            }

            asyncQueue.Dispose();

            // Check that the lists contain items in correct order.
            for (int i = 0; i < list1.Count - 1; i++)
                Assert.True(list1[i] < list1[i + 1]);

            for (int i = 0; i < list2.Count - 1; i++)
                Assert.True(list2[i] < list2[i + 1]);

            // Check that the lists contain all items when merged.
            list1.AddRange(list2);
            list1.Sort();

            for (int i = 0; i < list1.Count - 1; i++)
                Assert.Equal(list1[i] + 1, list1[i + 1]);

            // Check that all items were processed.
            Assert.Equal(list1.Count, itemsToProcess);
        }

        /// <summary>
        /// Worker of <see cref="AsyncQueue_DequeueParallelAsync"/> test that tries to consume items from the queue
        /// until the last item is reached or cancellation is triggered.
        /// </summary>
        /// <param name="asyncQueue">Queue to consume items from.</param>
        /// <param name="list">List to add consumed items to.</param>
        /// <param name="lastItem">Value of the last item that will be added to the queue.</param>
        /// <param name="cts">Cancellation source to cancel when we are done.</param>
        private async Task AsyncQueue_DequeueParallelAsync_WorkerAsync(AsyncQueue<int> asyncQueue, List<int> list, int lastItem, CancellationTokenSource cts)
        {
            while (true)
            {
                try
                {
                    int item = await asyncQueue.DequeueAsync(cts.Token);

                    await Task.Delay(this.random.Next(10));

                    list.Add(item);

                    // If we reached the last item, signal cancel to the other worker and finish.
                    if (item == lastItem)
                    {
                        cts.Cancel();
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.DequeueAsync(CancellationToken)"/> throws 
        /// exception when it is called on a queue operating in callback mode.
        /// </summary>
        [Fact]
        public async Task AsyncQueue_DequeueThrowsInCallbackMode()
        {
            var asyncQueue = new AsyncQueue<int>((item, cancellation) =>
            {
                return Task.CompletedTask;
            });

            // Enqueue an item, which should trigger the callback.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await asyncQueue.DequeueAsync());
        }

        /// <summary>
        /// Tests that <see cref="AsyncQueue{T}.Dispose"/> can be called from a callback.
        /// </summary>
        [Fact]
        public async void AsyncQueue_CanDisposeFromCallback_Async()
        {
            bool firstRun = true;
            bool shouldBeFalse = false;
            var asyncQueue = new AsyncQueue<IDisposable>((item, cancellation) =>
            {
                if (firstRun)
                {
                    item.Dispose();
                    firstRun = false;
                }
                else
                {
                    // This should not happen.
                    shouldBeFalse = true;
                }
                return Task.CompletedTask;
            });

            asyncQueue.Enqueue(asyncQueue);

            // We wait until the queue callback calling consumer is finished.
            asyncQueue.ConsumerTask.Wait();
            
            // Now enqueuing another item should not invoke the callback because the queue should be disposed.
            asyncQueue.Enqueue(asyncQueue);

            await Task.Delay(500);
            Assert.False(shouldBeFalse);
        }
    }
}
