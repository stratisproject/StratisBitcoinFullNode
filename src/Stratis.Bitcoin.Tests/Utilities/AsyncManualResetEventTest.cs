﻿using System;
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
        /// <summary>Source of randomness.</summary>
        private Random random = new Random();

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
            int tasksCount = 10;
            var cts = new CancellationTokenSource();

            var events = new List<AsyncManualResetEvent>();
            for (int i = 0; i < tasksCount; i++)
                events.Add(new AsyncManualResetEvent(false));

            var tasks = new List<Task>();

            List<int> resultList = new List<int>() { 0 };
            for (int i = 0; i < tasksCount; i++)
            {
                tasks.Add(AsyncManualResetEvent_RingTriggeringAsync_WorkerAsync(i, events, resultList, cts));
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

            for (int i = 0; i < resultList.Count; i++)
                Assert.Equal(i, resultList[i]);
        }

        private async Task AsyncManualResetEvent_RingTriggeringAsync_WorkerAsync(int id, List<AsyncManualResetEvent> events, List<int> resultList, CancellationTokenSource shutdown)
        {
            AsyncManualResetEvent selfEvent = events[id];
            while (!shutdown.IsCancellationRequested)
            {
                await selfEvent.WaitAsync(shutdown.Token);

                int next = resultList.Last() + 1;

                await Task.Delay(id + 1);

                resultList.Add(next);

                if (next == 250)
                    shutdown.Cancel();

                selfEvent.Reset();

                int nextId = this.random.Next(events.Count);
                events[nextId].Set();
            }
        }
    }
}
