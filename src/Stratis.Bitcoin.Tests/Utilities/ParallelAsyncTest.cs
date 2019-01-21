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
            this.testCollection = new List<int>();
            for (int i=0; i < 100; ++i)
                this.testCollection.Add(i);

            this.itemProcessingDelayMs = 50;

            this.testCollectionSum = 0;

            foreach (int item in this.testCollection)
                this.testCollectionSum += item;
        }

        [Fact]
        public async void ForEachAsync_TestDegreeOfParallelism_Async()
        {
            int sum = 0;

            Stopwatch watch = Stopwatch.StartNew();

            await this.testCollection.ForEachAsync(10, CancellationToken.None, async (item, cancellation) =>
            {
                Interlocked.Add(ref sum, item);
                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);
            }).ConfigureAwait(false);

            watch.Stop();
            Assert.True(watch.Elapsed.TotalMilliseconds < this.testCollection.Count * this.itemProcessingDelayMs);
            Assert.Equal(this.testCollectionSum, sum);
        }

        [Fact]
        public async void ForEachAsync_TestDegreeOfParallelism2_Async()
        {
            int sum = 0;

            Stopwatch watch = Stopwatch.StartNew();

            await this.testCollection.ForEachAsync(this.testCollection.Count, CancellationToken.None, async (item, cancellation) =>
            {
                Interlocked.Add(ref sum, item);
                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);
            }).ConfigureAwait(false);

            watch.Stop();
            Assert.True(watch.Elapsed.TotalMilliseconds < this.testCollection.Count * this.itemProcessingDelayMs);
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
                Interlocked.Increment(ref itemsProcessed);

                await Task.Delay(this.itemProcessingDelayMs).ConfigureAwait(false);

                if (itemsProcessed == 3)
                    tokenSource.Cancel();
            }).ConfigureAwait(false);

            Assert.Equal(3, itemsProcessed);
        }
    }
}
