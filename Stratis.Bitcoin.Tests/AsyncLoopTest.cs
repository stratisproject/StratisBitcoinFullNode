using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class AsyncLoopTest : LogsTestBase
    {
        private int iterationCount;

        public AsyncLoopTest() : base()
        {
            this.iterationCount = 0;
        }

        [Fact]
        public async Task RunOperationCanceledExceptionThrownBeforeCancellationTokenIsCancelledLogsException()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await DoOperationCanceledExceptionTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(80).Token, TimeSpan.FromMilliseconds(33));

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<OperationCanceledException>(this.FullNodeLogger, LogLevel.Critical, "This should not block the task from continuing.", "TestLoop threw an unhandled exception");
            Assert.Equal(1, this.iterationCount);
        }

        [Fact]
        public async Task RunWithoutCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await DoExceptionalTask(token);
            });

            await asyncLoop.Run(TimeSpan.FromMilliseconds(33));

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(this.FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, this.iterationCount);
        }

        [Fact]
        public async Task RunWithCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await DoExceptionalTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(150).Token, TimeSpan.FromMilliseconds(33));

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(this.FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, this.iterationCount);
        }

        [Fact]
        public async Task RunLogsStartAndStop()
        {
            var asyncLoop = new AsyncLoop("TestLoop", this.FullNodeLogger.Object, async token =>
            {
                await DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33));

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
        }

        [Fact]
        public async Task RunWithoutDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, async token =>
            {
                await DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(90).Token, TimeSpan.FromMilliseconds(33));

            Assert.Equal(3, this.iterationCount);
        }

        [Fact]
        public async Task RunWithDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", NullLogger.Instance, async token =>
            {
                await DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33), TimeSpan.FromMilliseconds(40));

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
