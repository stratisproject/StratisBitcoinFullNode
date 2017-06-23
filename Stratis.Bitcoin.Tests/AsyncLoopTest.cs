using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Tests.Logging;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class AsyncLoopTest : LogsTestBase
    {
        private int iterationCount;

        public AsyncLoopTest() : base()
        {
            iterationCount = 0;
        }

        [Fact]
        public async Task RunOperationCanceledExceptionThrownBeforeCancellationTokenIsCancelledLogsException()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoOperationCanceledExceptionTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(80).Token, TimeSpan.FromMilliseconds(33));

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<OperationCanceledException>(FullNodeLogger, LogLevel.Critical, "This should not block the task from continuing.", "TestLoop threw an unhandled exception");
            Assert.Equal(1, iterationCount);
        }

        [Fact]
        public async Task RunWithoutCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoExceptionalTask(token);
            });

            await asyncLoop.Run(TimeSpan.FromMilliseconds(33));

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, iterationCount);
        }

        [Fact]
        public async Task RunWithCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoExceptionalTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(150).Token, TimeSpan.FromMilliseconds(33));

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, iterationCount);
        }

        [Fact]
        public async Task RunLogsStartAndStop()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33));

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
        }

        [Fact]
        public async Task RunWithoutDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(90).Token, TimeSpan.FromMilliseconds(33));

            Assert.Equal(3, iterationCount);
        }

        [Fact]
        public async Task RunWithDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            await asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33), TimeSpan.FromMilliseconds(40));

            Assert.Equal(2, iterationCount);
        }

        private Task DoExceptionalTask(CancellationToken token)
        {
            iterationCount++;

            if (iterationCount == 3)
            {
                throw new InvalidOperationException("Cannot run more than 3 times.");
            }

            return Task.CompletedTask;
        }

        private Task DoOperationCanceledExceptionTask(CancellationToken token)
        {
            iterationCount++;

            if (iterationCount == 1)
            {
                throw new OperationCanceledException("This should not block the task from continuing.");
            }

            return Task.CompletedTask;
        }

        private Task DoTask(CancellationToken token)
        {
            iterationCount++;
            return Task.CompletedTask;
        }
    }
}
