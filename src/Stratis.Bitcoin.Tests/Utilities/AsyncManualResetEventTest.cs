using System;
using System.Collections.Generic;
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

            Task task = Task.Run(async delegate
            {
                await Task.Delay(500);
                manualResetEvent.Set();
            });

            await manualResetEvent.WaitAsync();
        }

        [Fact]
        public void AsyncManualResetEvent_CanReset()
        {
            AsyncManualResetEvent manualResetEvent = new AsyncManualResetEvent(true);

            Assert.True(manualResetEvent.IsSet);

            manualResetEvent.Reset();

            Assert.False(manualResetEvent.IsSet);
        }

        [Fact]
        public async Task AsyncManualResetEvent_IsNotCompletedAsync()
        {
            var mre = new AsyncManualResetEvent();

            var task = mre.WaitAsync();

            await Task.Delay(200);

            Assert.False(task.IsCompleted);
        }

        [Fact]
        public void AsyncManualResetEvent_AfterSet_IsCompleted()
        {
            var mre = new AsyncManualResetEvent();

            mre.Set();
            var task = mre.WaitAsync();

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public void AsyncManualResetEvent_Set_IsCompleted()
        {
            var mre = new AsyncManualResetEvent(true);

            var task = mre.WaitAsync();

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
            var task = mre.WaitAsync();

            await Task.Delay(200);

            Assert.False(task.IsCompleted);
        }
    }
}
