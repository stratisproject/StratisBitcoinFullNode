using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AsyncManualResetEventTest
    {
        /// <summary>Used in <see cref="AsyncManualResetEvent_RingTriggeringAsync"/>.</summary>
        private int counter;

        [Fact]
        public async void AsyncManualResetEvent_WaitAsync()
        {
            AsyncManualResetEvent manualResetEvent = new AsyncManualResetEvent(false);

            Stopwatch stopwatch = Stopwatch.StartNew();

            Task task = Task.Run(async () =>
            {
                await Task.Delay(500);
                manualResetEvent.Set();
            });

            Task mreAwaitingTask = manualResetEvent.WaitAsync();
            await mreAwaitingTask;

            stopwatch.Stop();

            Assert.True(stopwatch.ElapsedMilliseconds >= 500);
            Assert.True(mreAwaitingTask.Status == TaskStatus.RanToCompletion);
        }

        [Fact]
        public void AsyncManualResetEvent_CanSetAndReset()
        {
            AsyncManualResetEvent manualResetEvent = new AsyncManualResetEvent(false);

            Assert.False(manualResetEvent.IsSet);

            manualResetEvent.Set();

            Assert.True(manualResetEvent.IsSet);

            manualResetEvent.Reset();

            Assert.False(manualResetEvent.IsSet);
        }

        [Fact]
        public async Task AsyncManualResetEvent_IsNotCompletedAsync()
        {
            var mre = new AsyncManualResetEvent();

            Task task = mre.WaitAsync();

            await Task.Delay(200);

            Assert.False(task.IsCompleted);
        }

        [Fact]
        public void AsyncManualResetEvent_AfterSet_IsCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            Task task = mre.WaitAsync();

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void AsyncManualResetEvent_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            Task task = mre.WaitAsync();

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void AsyncManualResetEvent_MultipleWaitAsync_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            var task1 = mre.WaitAsync();
            var task2 = mre.WaitAsync();

            Assert.True(task1.IsCompleted);
            Assert.True(task2.IsCompleted);
        }

        [Fact]
        public async Task AsyncManualResetEvent_AfterReset_IsNotCompletedAsync()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            mre.Reset();
            Task task = mre.WaitAsync();

            await Task.Delay(200);

            Assert.False(task.IsCompleted);
        }

        [Fact]
        public async void AsyncManualResetEvent_CanBeCancelledAsync()
        {
            var manualResetEvent = new AsyncManualResetEvent(false);
            var tokenSource = new CancellationTokenSource(500);

            Task mreAwaitingTask = manualResetEvent.WaitAsync(tokenSource.Token);

            try
            {
                await mreAwaitingTask;
            }
            catch (TaskCanceledException)
            {
            }

            Assert.True(mreAwaitingTask.Status == TaskStatus.Canceled);
            tokenSource.Dispose();
        }


        /// <summary>
        /// This test simulates several tasks that wait for it's own event and set next one.
        /// </summary>
        [Fact]
        public async void AsyncManualResetEvent_RingTriggeringAsync()
        {
            this.counter = 0;
            int tasksCount = 10;
            var cts = new CancellationTokenSource();

            var events = new List<AsyncManualResetEvent>();
            for (int i = 0; i < tasksCount; ++i)
                events.Add(new AsyncManualResetEvent(false));

            var tasks = new List<Task>();

            for (int i = 0; i < tasksCount; ++i)
            {
                AsyncManualResetEvent partnerEvent = i != (tasksCount - 1) ? events[i+1] : events[0];
                tasks.Add(CountAsync(events[i], partnerEvent, cts));
            }

            // Trigger one event.
            events.First().Set();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (TaskCanceledException)
            {
            }

            cts.Dispose();

            Assert.Equal(1000, this.counter);
        }

        private async Task CountAsync(AsyncManualResetEvent self, AsyncManualResetEvent partnerEvent, CancellationTokenSource shutdown)
        {
            while (!shutdown.IsCancellationRequested)
            {
                await self.WaitAsync(shutdown.Token);

                this.counter++;

                if (this.counter >= 1000)
                    shutdown.Cancel();

                self.Reset();
                partnerEvent.Set();
            }
        }
    }
}
