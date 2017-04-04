using System;
using System.Collections.Generic;
using System.Linq;
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
        public void RunOperationCanceledExceptionThrownBeforeCancellationTokenIsCancelledLogsException()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoOperationCanceledExceptionTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(80).Token, TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<OperationCanceledException>(FullNodeLogger, LogLevel.Critical, "This should not block the task from continuing.", "TestLoop threw an unhandled exception");
            Assert.Equal(1, iterationCount);
        }

        [Fact]
        public void RunWithoutCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoExceptionalTask(token);
            });

            asyncLoop.Run(TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, iterationCount);
        }

        [Fact]
        public void RunWithCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoExceptionalTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(150).Token, TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.Equal(3, iterationCount);         
        }

        [Fact]
        public void RunLogsStartAndStop()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(FullNodeLogger, LogLevel.Information, "TestLoop stopping");
        }

        [Fact]
        public void RunWithoutDelayRunsTaskUntilCancelled()
        {            
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });
            
            asyncLoop.Run(new CancellationTokenSource(90).Token, TimeSpan.FromMilliseconds(33)).Wait();

            Assert.Equal(3, iterationCount);
        }

        [Fact]
        public void RunWithDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33), TimeSpan.FromMilliseconds(40)).Wait();

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
