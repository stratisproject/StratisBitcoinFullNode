using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class AsyncLoopTest : LogsTestBase
    {
        private int iterationCount;

        public AsyncLoopTest() : base()
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

            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
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

            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
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

            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
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

            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
        }

        [Fact]
        public async Task RunWithoutDelayRunsTaskUntilCancelledAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, async token =>
            {
                await this.DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(900).Token, TimeSpan.FromMilliseconds(330)).RunningTask;

            Assert.Equal(3, this.iterationCount);
        }

        [Fact]
        public async Task RunWithDelayRunsTaskUntilCancelledAsync()
        {
            var asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, async token =>
            {
                await this.DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(1000).Token, TimeSpan.FromMilliseconds(330), TimeSpan.FromMilliseconds(400)).RunningTask;

            Assert.Equal(2, this.iterationCount);
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
