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
    public class PeriodicTaskTest : LogsTestBase
    {
        private int iterationCount;

        /// <summary>
        /// Unable to tests background thread exception throwing due to https://github.com/xunit/xunit/issues/157.
        /// </summary>
        public PeriodicTaskTest() : base()
        {
            this.iterationCount = 0;
        }

        [Fact]
        [Trait("Unstable", "True")]
        public void StartLogsStartAndStop()
        {
            var periodicTask = new PeriodicTask("TestTask", this.FullNodeLogger.Object, async token =>
            {
                await this.DoTask(token);
            });

            periodicTask.Start(new CancellationTokenSource(100).Token, TimeSpan.FromMilliseconds(33));

            Thread.Sleep(120);
            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestTask starting.");
            this.AssertLog(this.FullNodeLogger, LogLevel.Information, "TestTask stopping.");
        }

        [Fact]
        public void StartWithoutDelayRunsTaskUntilCancelled()
        {
            var periodicTask = new PeriodicTask("TestTask", NullLogger.Instance, async token =>
            {
                await this.DoTask(token);
            });

            periodicTask.Start(new CancellationTokenSource(800).Token, TimeSpan.FromMilliseconds(300));

            Thread.Sleep(2000);
            Assert.True(this.iterationCount > 1);
        }

        [Fact]
        public void StartWithDelayUsesIntervalForDelayRunsTaskUntilCancelled()
        {
            var periodicTask = new PeriodicTask("TestTask", NullLogger.Instance, async token =>
            {
                await this.DoTask(token);
            });

            periodicTask.Start(new CancellationTokenSource(800).Token, TimeSpan.FromMilliseconds(300), true);

            Thread.Sleep(1000);
            Assert.Equal(2, this.iterationCount);
        }

        [Fact]
        public void RunOnceDoesOneExecutionCycle()
        {
            var periodicTask = new PeriodicTask("TestTask", NullLogger.Instance, async token =>
            {
                await this.DoTask(token);
            });

            periodicTask.RunOnce();

            Assert.Equal(1, this.iterationCount);
        }

        private Task DoTask(CancellationToken token)
        {
            Interlocked.Increment(ref this.iterationCount);
            return Task.CompletedTask;
        }
    }
}
