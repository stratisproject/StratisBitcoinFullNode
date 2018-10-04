using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AsyncLoopTest : LogsTestBase
    {
        private int iterationCount;

        public AsyncLoopTest()
        {
            this.iterationCount = 0;
        }

        [Fact]
        public async Task RunOperationCanceledExceptionThrownBeforeCancellationTokenIsCancelledLogsExceptionAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await this.DoOperationCanceledExceptionTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(800).Token, TimeSpan.FromMilliseconds(300)).RunningTask;

            this.AssertLog<OperationCanceledException>(this.FullNodeLogger, LogLevel.Critical, "This should not block the task from continuing.", "TestLoop threw an unhandled exception");
            Assert.Equal(1, this.iterationCount);
        }

        [Fact]
        public async Task RunWithoutCancellationTokenRunsUntilExceptionOccursAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await this.DoExceptionalTask(token);
            });

            await asyncLoop.Run(TimeSpan.FromMilliseconds(330)).RunningTask;

            this.AssertLog<InvalidOperationException>(this.FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, this.iterationCount);
        }

        [Fact]
        public async Task RunWithCancellationTokenRunsUntilExceptionOccursAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await this.DoExceptionalTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(1500).Token, TimeSpan.FromMilliseconds(330)).RunningTask;

            this.AssertLog<InvalidOperationException>(this.FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, this.iterationCount);
        }

        [Fact]
        public async Task RunLogsStartAndStopAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await this.DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(1000).Token, TimeSpan.FromMilliseconds(330)).RunningTask;
        }

        [Fact]
        public async Task RunWithoutDelayRunsTaskUntilCancelledAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, async token =>
            {
                this.iterationCount++;
                await Task.CompletedTask;
            });

            await asyncLoop.Run(new CancellationTokenSource(900).Token, TimeSpan.FromMilliseconds(330)).RunningTask;

            Assert.True(this.iterationCount > 1);
        }

        [Fact]
        public async Task RunWithDelayRunsTaskUntilCancelledAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, async token =>
            {
                await this.DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(1000).Token, TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(100)).RunningTask;

            Assert.True(this.iterationCount > 1);
        }

        [Fact]
        public async Task AsyncLoopRepeatEveryIntervalCanBeChangedWhileRunningAsync()
        {
            int iterations = 0;

            IAsyncLoop asyncLoop = null;

            asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, token =>
            {
                iterations++;

                if (iterations == 2)
                    asyncLoop.RepeatEvery = TimeSpan.FromMilliseconds(200);

                return Task.CompletedTask;
            });

            Task loopRun = asyncLoop.Run(new CancellationTokenSource(5000).Token, TimeSpan.FromMilliseconds(1000)).RunningTask;

            await loopRun;

            Assert.True(iterations >= 6);
        }

        private Task DoExceptionalTask(CancellationToken token)
        {
            this.iterationCount++;

            if (this.iterationCount == 3)
            {
                throw new InvalidOperationException("Cannot run more than 3 times.");
            }

            return Task.CompletedTask;
        }

        private Task DoOperationCanceledExceptionTask(CancellationToken token)
        {
            this.iterationCount++;

            if (this.iterationCount == 1)
            {
                throw new OperationCanceledException("This should not block the task from continuing.");
            }

            return Task.CompletedTask;
        }

        private Task DoTask(CancellationToken token)
        {
            this.iterationCount++;
            return Task.CompletedTask;
        }
    }
}
