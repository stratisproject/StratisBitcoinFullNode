using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Tests of <see cref="TimeSyncBehavior"/> and <see cref="TimeSyncBehaviorState"/> classes.
    /// </summary>
    public class TimeSyncBehaviorTest
    {
        /// <summary>
        /// Number of milliseconds that two subsequent trivial calls should executed within.
        /// <para>This is used to evaluate whether there is any time difference between adjusted time and normal time.</para>
        /// </summary>
        private const int TimeEpsilonMs = 50;

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// properly calculates adjusted time offset using small sample set.
        /// </summary>
        [Fact]
        public void AddTimeData_WithSmallSampleSet_CalculatedCorrectly()
        {
            // Samples to be inserted to the state.
            // Columns meanings: isInbound, isUsed, expectedTimeOffsetLessThanMs, timeOffsetSample, peerAddress
            var samples = new List<object[]>
            {
                // First group of samples does not affect adjusted time, so difference should be ~0 ms.
                new object[] { false, true,     0, TimeSpan.FromSeconds(3.56),      IPAddress.Parse("1.2.3.4"),                                 },
                new object[] { true,  true,     0, TimeSpan.FromSeconds(13.123),    IPAddress.Parse("1.2.3.4"),                                 },
                new object[] { false, false,    0, TimeSpan.FromSeconds(7.123),     IPAddress.Parse("1.2.3.4"),                                 },
                new object[] { false, true,     0, TimeSpan.FromSeconds(26.0),      IPAddress.Parse("2001:0db8:85a3:1232:0000:8a2e:0370:7334"), },
                new object[] { false, false,    0, TimeSpan.FromSeconds(260),       IPAddress.Parse("2001:0db8:85a3:1232:0000:8a2e:0370:7334"), },
                new object[] { false, true,     0, TimeSpan.FromSeconds(-2126.0),   IPAddress.Parse("1.2.3.5"),                                 },
                new object[] { true,  true,     0, TimeSpan.FromSeconds(-391),      IPAddress.Parse("1.2.3.45"),                                },

                // These samples will change adjusted time.
                new object[] { false, true,  1280, TimeSpan.FromSeconds(-1),        IPAddress.Parse("2001:0db8:85a3:0000:0000:8a2e:0370:7334"), },
                new object[] { true,  true,  3560, TimeSpan.FromSeconds(23.6),      IPAddress.Parse("::1"),                                     },
                new object[] { true,  true,  3560, TimeSpan.FromSeconds(236),       IPAddress.Parse("12.2.3.5"),                                },
                new object[] { false, true,  1236, TimeSpan.FromSeconds(1.236),     IPAddress.Parse("13.2.3.5"),                                },
                new object[] { true,  true,  1236, TimeSpan.FromSeconds(-1000),     IPAddress.Parse("14.2.3.5"),                                },
                new object[] { true,  true,  1236, TimeSpan.FromSeconds(-4.9236),   IPAddress.Parse("15.2.3.5"),                                },
                new object[] { false, true,   118, TimeSpan.FromSeconds(-4444.444), IPAddress.Parse("16.2.3.5"),                                },
            };

            var dateTimeProvider = DateTimeProvider.Default;
            var lifetime = new NodeLifetime();
            var loggerFactory = new LoggerFactory();
            var asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            var state = new TimeSyncBehaviorState(dateTimeProvider, lifetime, asyncLoopFactory, loggerFactory);

            for (int i = 0; i < samples.Count; i++)
            {
                bool isInbound = (bool)samples[i][0];
                bool isUsed = (bool)samples[i][1];
                int expectedTimeOffsetLessThanMs = (int)samples[i][2];
                TimeSpan timeOffsetSample = (TimeSpan)samples[i][3];
                IPAddress peerAddress = (IPAddress)samples[i][4];

                bool used = state.AddTimeData(peerAddress, timeOffsetSample, isInbound);
                Assert.Equal(isUsed, used);

                DateTime adjustedTime = dateTimeProvider.GetAdjustedTime();
                DateTime normalTime = dateTimeProvider.GetUtcNow();
                TimeSpan diff = adjustedTime - normalTime;

                Assert.True(Math.Abs(diff.TotalMilliseconds) < expectedTimeOffsetLessThanMs + TimeEpsilonMs);
            }
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// turns on the user warnings and then switches off the time sync feature on defined threshold levels.
        /// </summary>
        [Fact]
        public void AddTimeData_WithSmallSampleSet_TurnsWarningOnAndSwitchesSyncOff()
        {
            // Samples to be inserted to the state.
            // Columns meanings: isInbound, isUsed, isWarningOn, isSyncOff, timeOffsetSample, peerAddress
            var samples = new List<object[]>
            {
                // First group of samples does not affect adjusted time, so difference should be ~0 ms.
                new object[] { false, true,  false, false, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 1),      IPAddress.Parse("1.2.3.41"), },
                new object[] { false, true,  false, false, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 2),      IPAddress.Parse("1.2.3.42"), },
                new object[] { false, true,  false, false, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 3),      IPAddress.Parse("1.2.3.43"), },

                // The next sample turns on the warning.
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 4),      IPAddress.Parse("1.2.3.44"), },

                // It can't be turned off.
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(3),                                                                IPAddress.Parse("1.2.3.45"), },
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(4),                                                                IPAddress.Parse("1.2.3.46"), },

                // Add more samples.
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 10),                 IPAddress.Parse("1.2.3.47"), },
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 11),                 IPAddress.Parse("1.2.3.48"), },
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 12),                 IPAddress.Parse("1.2.3.49"), },
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 13),                 IPAddress.Parse("1.2.31.4"), },
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 14),                 IPAddress.Parse("1.2.32.4"), },
                new object[] { false, true,  true,  false, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 15),                 IPAddress.Parse("1.2.33.4"), },

                // Now the feature should be turned off.
                new object[] { true,  true,  true,  true,  TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 16),                 IPAddress.Parse("1.2.33.4"), },

                // No more samples should be accepted now.
                new object[] { false, false, true,  true,  TimeSpan.FromSeconds(2),                                                                IPAddress.Parse("1.2.34.4"), },
                new object[] { false, false, true,  true,  TimeSpan.FromSeconds(1),                                                                IPAddress.Parse("1.2.35.4"), },
            };

            var dateTimeProvider = DateTimeProvider.Default;
            var lifetime = new NodeLifetime();
            var loggerFactory = new LoggerFactory();
            var asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            var state = new TimeSyncBehaviorState(dateTimeProvider, lifetime, asyncLoopFactory, loggerFactory);

            for (int i = 0; i < samples.Count; i++)
            {
                bool isInbound = (bool)samples[i][0];
                bool isUsed = (bool)samples[i][1];
                bool isWarningOn = (bool)samples[i][2];
                bool isSyncOff = (bool)samples[i][3];
                TimeSpan timeOffsetSample = (TimeSpan)samples[i][4];
                IPAddress peerAddress = (IPAddress)samples[i][5];

                bool used = state.AddTimeData(peerAddress, timeOffsetSample, isInbound);
                Assert.Equal(isUsed, used);

                Assert.Equal(isWarningOn, state.WarningLoopStarted);
                Assert.Equal(isSyncOff, state.SwitchedOffLimitReached);
                Assert.Equal(isSyncOff, state.SwitchedOff);

                if (state.SwitchedOff)
                {
                    DateTime adjustedTime = dateTimeProvider.GetAdjustedTime();
                    DateTime normalTime = dateTimeProvider.GetUtcNow();
                    TimeSpan diff = adjustedTime - normalTime;
                    Assert.True(Math.Abs(diff.TotalMilliseconds) < TimeEpsilonMs);
                }
            }
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// forgets old samples if it has reached predefined limits.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSet_ForgetsOldSamples()
        {
            int inboundSamples = 300;
            int outboundSamples = 300;

            var dateTimeProvider = DateTimeProvider.Default;
            var lifetime = new NodeLifetime();
            var loggerFactory = new LoggerFactory();
            var asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            var state = new TimeSyncBehaviorState(dateTimeProvider, lifetime, asyncLoopFactory, loggerFactory);

            var inSamples = new List<int>();
            for (int i = 0; i < inboundSamples; i++)
            {
                IPAddress peerAddress = new IPAddress(i);
                bool used = state.AddTimeData(peerAddress, TimeSpan.FromSeconds(i), true);
                Assert.True(used);
                if (i >= inboundSamples - TimeSyncBehaviorState.MaxInboundSamples) inSamples.Add(i * 1000);
            }

            DateTime adjustedTime = dateTimeProvider.GetAdjustedTime();
            DateTime normalTime = dateTimeProvider.GetUtcNow();
            TimeSpan diff = adjustedTime - normalTime;
            int expectedDiffMs = inSamples.Median();
            Assert.True(Math.Abs(diff.TotalMilliseconds) < Math.Abs(expectedDiffMs) + TimeEpsilonMs);

            var outSamples = new List<int>();
            for (int i = 0; i < outboundSamples; i++)
            {
                IPAddress peerAddress = new IPAddress(i);
                bool used = state.AddTimeData(peerAddress, TimeSpan.FromSeconds(-i), false);
                Assert.True(used);
                if (i >= outboundSamples - TimeSyncBehaviorState.MaxOutboundSamples) outSamples.Add(-i * 1000);
            }

            var allSamples = new List<int>(inSamples);
            for (int i = 0; i < TimeSyncBehaviorState.OutboundToInboundWeightRatio; i++)
              allSamples.AddRange(outSamples);

            adjustedTime = dateTimeProvider.GetAdjustedTime();
            normalTime = dateTimeProvider.GetUtcNow();
            diff = adjustedTime - normalTime;
            expectedDiffMs = allSamples.Median();
            Assert.True(Math.Abs(diff.TotalMilliseconds) < Math.Abs(expectedDiffMs) + TimeEpsilonMs);
        }
    }
}
