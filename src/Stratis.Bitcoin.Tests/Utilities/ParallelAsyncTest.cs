using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class ParallelAsyncTest
    {
        private readonly List<int> testCollection;
        private readonly int itemProcessingDelayMs;
        private readonly int testCollectionSum;

        public ParallelAsyncTest()
        {
            this.testCollection = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8 };
            this.itemProcessingDelayMs = 200;

            this.testCollectionSum = 0;

            foreach (int item in this.testCollection)
                this.testCollectionSum += item;
        }

        [Fact]
        public async void ForEachAsync_TestDegreeOfParallelism_Async()
        {
            int sum = 0;

            var delayTask = new Task(async () => { await Task.Delay(this.testCollection.Count * this.itemProcessingDelayMs / 2).ConfigureAwait(false); });

            await this.testCollection.ForEachAsync(2, CancellationToken.None, async (item, cancellation) =>
            {
                sum += item;
                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);
            }).ConfigureAwait(false);

            Assert.False(delayTask.IsCompleted);
            Assert.Equal(this.testCollectionSum, sum);
        }

        [Fact]
        public async void ForEachAsync_TestDegreeOfParallelism2_Async()
        {
            int sum = 0;
            
            var delayTask = new Task(async () => { await Task.Delay(this.testCollection.Count * this.itemProcessingDelayMs).ConfigureAwait(false); });
            
            await this.testCollection.ForEachAsync(this.testCollection.Count, CancellationToken.None, async (item, cancellation) =>
            {
                sum += item;
                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);
            }).ConfigureAwait(false);

            // We expect ForEachAsync task to be finished well before the delay task.
            Assert.False(delayTask.IsCompleted);
            Assert.Equal(this.testCollectionSum, sum);
        }

        [Fact]
        public async void ForEachAsync_DoesNothingWithEmptyCollection_Async()
        {
            var emptyList = new List<int>();

            await emptyList.ForEachAsync(2, CancellationToken.None, async (item, cancellation) =>
            {
                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        
        [Fact]
        public async void ForEachAsync_CanBeCancelled_Async()
        {
            var tokenSource = new CancellationTokenSource();

            int itemsProcessed = 0;

            await this.testCollection.ForEachAsync(1, tokenSource.Token, async (item, cancellation) =>
            {
                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);
                itemsProcessed++;

                if (itemsProcessed == 3)
                    tokenSource.Cancel();
            }).ConfigureAwait(false);
            
            Assert.Equal(3, itemsProcessed);
        }
    }
}
