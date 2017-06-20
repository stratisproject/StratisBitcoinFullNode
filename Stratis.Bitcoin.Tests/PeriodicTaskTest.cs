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
    public class PeriodicTaskTest : LogsTestBase
    {
        private int iterationCount;

        /// <remarks>
        /// Unable to tests background thread exception throwing due to https://github.com/xunit/xunit/issues/157.
        /// </remarks>
        protected override void Initialize()
        {
            this.iterationCount = 0;
        }

        [TestMethod]
        public void StartLogsStartAndStop()
        {
            var periodicTask = new PeriodicTask("TestTask", async token =>
            {
                await DoTask(token);
            });

            periodicTask.Start(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33));

            Thread.Sleep(120);            
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestTask starting");
            AssertLog(this.FullNodeLogger, LogLevel.Information, "TestTask stopping");
        }

        [TestMethod]
        public void StartWithoutDelayRunsTaskUntilCancelled()
        {
            var periodicTask = new PeriodicTask("TestTask", async token =>
            {
                await DoTask(token);
            });

            periodicTask.Start(new CancellationTokenSource(80).Token, TimeSpan.FromMilliseconds(33));

            Thread.Sleep(100);
            Assert.AreEqual(3, this.iterationCount);
        }

        [TestMethod]
        public void StartWithDelayUsesIntervalForDelayRunsTaskUntilCancelled()
        {
            var periodicTask = new PeriodicTask("TestTask", async token =>
            {
                await DoTask(token);
            });

            periodicTask.Start(new CancellationTokenSource(70).Token, TimeSpan.FromMilliseconds(33), true);

            Thread.Sleep(100);
            Assert.AreEqual(2, this.iterationCount);
        }

        [TestMethod]
        public void RunOnceDoesOneExecutionCycle()
        {
            var periodicTask = new PeriodicTask("TestTask", async token =>
            {
                await DoTask(token);
            });

            periodicTask.RunOnce();

            Assert.AreEqual(1, this.iterationCount);
        }

        private Task DoExceptionalTask(CancellationToken token)
        {
            Interlocked.Increment(ref this.iterationCount);

            if (this.iterationCount == 3)
            {
                throw new InvalidOperationException("Cannot run more than 3 times.");
            }

            return Task.CompletedTask;
        }


        private Task DoTask(CancellationToken token)
        {
            Interlocked.Increment(ref this.iterationCount);
            return Task.CompletedTask;
        }
    }
}