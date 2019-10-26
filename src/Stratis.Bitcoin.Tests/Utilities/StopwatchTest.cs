using System;
using System.Runtime.InteropServices;
using System.Threading;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of <see cref="StopwatchDisposable"/> class.
    /// </summary>
    public class StopwatchTest
    {
        /// <summary>
        /// Previously the disposable stopwatch implementation was based on <see cref="System.DateTimeOffset.UtcNow"/>
        /// instead of <see cref="System.Diagnostics.Stopwatch"/>. It was argued that there was some kind of measurement
        /// error when the later was used. Most likely the problem was in mixing <see cref="System.DateTime.Ticks"/>
        /// units with incompatible <see cref="System.Diagnostics.Stopwatch.ElapsedTicks"/>.
        /// Issue that cover this subject is <see href="https://github.com/stratisproject/StratisBitcoinFullNode/issues/2391"/>.
        /// <para>
        /// This test aims to verify that the time measurement with the disposable stopwatch achieves correct results.
        /// It performs a series of small work simlating delays which represent a measured code block. Each delay
        /// is measured using 4 different measurement methods and the total elapsed time of all three methods is then compared.
        /// It is expected that all 4 methods will produce roughly the same results.
        /// </para>
        /// <para>
        /// The first method we use is using two <see cref="System.DateTime.UtcNow"/> calls, one done before and one done after.
        /// The second method is using <see cref="System.Diagnostics.Stopwatch"/>, which we start before the work and stop after
        /// the work is done. The third method is using the actual disposable watch that we want to test. The fourth method
        /// is just calculating the expected delay without actually measuring it.
        /// </para>
        /// </summary>
        [Fact]
        public void StopwatchDisposable_MeasuresPerformanceCorrectly()
        {
            // Don't run this test in a Mac environment as it takes too long,
            // skewing the results.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return;

            // Give the testing environment a chance to settle down a little bit.
            Thread.Sleep(5000);

            int epsilonMs = 500;
            int expectedElapsedMs = 0;
            long elapsedTicksByDispStopwatch = 0;

            DateTime startTime = DateTime.UtcNow;
            var diagStopwatch = new System.Diagnostics.Stopwatch();

            int delayTimeMs = 0;
            var rnd = new Random();
            for (int i = 0; i < 10; i++)
            {
                int delay = rnd.Next(1000);
                expectedElapsedMs += delay;

                diagStopwatch.Start();

                using (new StopwatchDisposable(o => Interlocked.Add(ref elapsedTicksByDispStopwatch, o)))
                {
                    // Actual work that we want to measure, which is simulated by sleep.
                    Thread.Sleep(delay);
                }

                diagStopwatch.Stop();

                // Additional sleep between each instance of the work being measured.
                delay = rnd.Next(1000);
                delayTimeMs += delay;
                Thread.Sleep(delay);
            }

            DateTime endTime = DateTime.UtcNow.AddMilliseconds(-delayTimeMs);
            TimeSpan elapsedTimeByDateTime = endTime - startTime;
            var elapsedTimeByDiagStopwatch = new TimeSpan(diagStopwatch.Elapsed.Ticks);
            var elapsedTimeByDispStopwatch = new TimeSpan(elapsedTicksByDispStopwatch);
            TimeSpan elapsedTimeByCalculation = TimeSpan.FromMilliseconds(expectedElapsedMs);

            // Check that the measured times do not differ by more than "epsilonMs".
            double diffDispDateTime = Math.Abs((elapsedTimeByDispStopwatch - elapsedTimeByDateTime).TotalMilliseconds);
            double diffDispDiag = Math.Abs((elapsedTimeByDispStopwatch - elapsedTimeByDiagStopwatch).TotalMilliseconds);
            double diffDispCalculation = Math.Abs((elapsedTimeByDispStopwatch - elapsedTimeByCalculation).TotalMilliseconds);

            Assert.True(diffDispDateTime < epsilonMs);
            Assert.True(diffDispDiag < epsilonMs);
            Assert.True(diffDispCalculation < epsilonMs);
        }
    }
}
