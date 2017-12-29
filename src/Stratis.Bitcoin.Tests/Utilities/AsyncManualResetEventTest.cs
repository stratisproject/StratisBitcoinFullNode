using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AsyncManualResetEventTest
    {
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
    }
}
