using System;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AsyncConsumerQueueTest
    {
        [Fact]
        public async void QueueReactsToTriggerAsync()
        {
            int sum = 0;

            AsyncConsumerQueue<int> processedQueue = new AsyncConsumerQueue<int>(
                innerQueue =>
                {
                    while (innerQueue.TryDequeue(out int item))
                        sum += item;
                }, item =>
                {
                    // Triger if '5' gets added.
                    return item == 5;
                } );

            for (int i = 0; i < 20; i++)
            {
                processedQueue.Enqueue(i);
                await Task.Delay(50);
            }

            processedQueue.Dispose();

            // 0+1+2+3+4+5 == 15
            Assert.Equal(15, sum);
        }

        [Fact]
        public async void QueueReactsToTimerAsync()
        {
            int sum = 0;

            AsyncConsumerQueue<int> processedQueue = new AsyncConsumerQueue<int>(
                innerQueue =>
                {
                    while (innerQueue.TryDequeue(out int item))
                        sum += item;
                }, null,
                TimeSpan.FromMilliseconds(500));

            for (int i = 0; i < 5; i++)
            {
                processedQueue.Enqueue(i);
                await Task.Delay(50);
            }


            // 250 ms after the start no items were processed.
            Assert.Equal(5, processedQueue.Count);

            await Task.Delay(300);

            Assert.Equal(0, processedQueue.Count);
            Assert.Equal(10, sum);
            processedQueue.Dispose();
        }

        [Fact]
        public async void QueueReactsToTriggerAndTimerAsync()
        {
            int sum = 0;

            AsyncConsumerQueue<int> processedQueue = new AsyncConsumerQueue<int>(
                innerQueue =>
                {
                    while (innerQueue.TryDequeue(out int item))
                        sum += item;
                }, item =>
                {
                    // Triger if '5' gets added.
                    return item == 5;
                }, TimeSpan.FromMilliseconds(500));

            for (int i = 0; i < 6; i++)
            {
                processedQueue.Enqueue(i);
                await Task.Delay(30);
            }

            // 15 after being triggered.
            Assert.Equal(15, sum);


            for (int i = 6; i < 10; i++)
            {
                processedQueue.Enqueue(i);
                await Task.Delay(30);
            }

            await Task.Delay(500);

            // 45 after a timer.
            Assert.Equal(45, sum);
            processedQueue.Dispose();
        }

        [Fact]
        public async void TimerRestartsIfNotAllItemsWereConsumedAsync()
        {
            int sum = 0;

            AsyncConsumerQueue<int> processedQueue = new AsyncConsumerQueue<int>(
                innerQueue =>
                {
                    // Process only 3 items at a time.
                    int itemsProcessed = 0;
                    while (itemsProcessed < 3 && innerQueue.TryDequeue(out int item))
                    {
                        itemsProcessed++;
                        sum += item;
                    }
                }, null, TimeSpan.FromMilliseconds(300), true);

            for (int i = 0; i < 10; i++)
            {
                processedQueue.Enqueue(i);
                await Task.Delay(10);
            }

            await Task.Delay(1000);

            // All items are eventually processed.
            Assert.Equal(36, sum);
            processedQueue.Dispose();
        }
    }
}
