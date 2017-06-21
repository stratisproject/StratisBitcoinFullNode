using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Tests.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests
{
    [TestClass]
    public class AsyncLoopTest : LogsTestBase
    {
        private int iterationCount;

        protected override void Initialize()
        {
            this.iterationCount = 0;
        }

        [TestMethod]
        public void RunOperationCanceledExceptionThrownBeforeCancellationTokenIsCancelledLogsException()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoOperationCanceledExceptionTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(80).Token, TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<OperationCanceledException>(this.FullNodeLogger, LogLevel.Critical, "This should not block the task from continuing.", "TestLoop threw an unhandled exception");
            Assert.AreEqual(1, this.iterationCount);
        }

        [TestMethod]
        public void RunWithoutCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoExceptionalTask(token);
            });

            asyncLoop.Run(TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(this.FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.AreEqual(3, this.iterationCount);
        }

        [TestMethod]
        public void RunWithCancellationTokenRunsUntilExceptionOccurs()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoExceptionalTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(150).Token, TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
            AssertLog<InvalidOperationException>(this.FullNodeLogger, LogLevel.Critical, "Cannot run more than 3 times.", "TestLoop threw an unhandled exception");
            Assert.AreEqual(3, this.iterationCount);
        }

        [TestMethod]
        public void RunLogsStartAndStop()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33)).Wait();

            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestLoop stopping");
        }

        [TestMethod]
        public void RunWithoutDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(90).Token, TimeSpan.FromMilliseconds(33)).Wait();

            Assert.AreEqual(3, this.iterationCount);
        }

        [TestMethod]
        public void RunWithDelayRunsTaskUntilCancelled()
        {
            var asyncLoop = new AsyncLoop("TestLoop", async token =>
            {
                await DoTask(token);
            });

            asyncLoop.Run(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33), TimeSpan.FromMilliseconds(40)).Wait();

            Assert.AreEqual(2, this.iterationCount);
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
