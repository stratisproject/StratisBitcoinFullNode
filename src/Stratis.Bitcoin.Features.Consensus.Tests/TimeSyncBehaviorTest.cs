using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
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
            var samples = new List<TestSample>
            {
                // Columns meanings: expectedIsUsed, expectedIsWarningOn, expectedIsTimeSyncOff, expectedTimeOffsetMs, timeOffsetSample, peerAddress

                // First group of samples does not affect adjusted time, so difference should be ~0 ms.
                TestSample.Outbound(true,  false, false,    0, TimeSpan.FromSeconds(3.56),      new IPAddress(1)),
                TestSample.Inbound(true,   false, false,    0, TimeSpan.FromSeconds(13.123),    new IPAddress(1)),
                TestSample.Outbound(false, false, false,    0, TimeSpan.FromSeconds(7.123),     new IPAddress(1)), // IP address already used for outbound.
                TestSample.Outbound(true,  false, false,    0, TimeSpan.FromSeconds(26.0),      IPAddress.Parse("2000:0db8:85a3:1232:0000:8a2e:0370:7334")),
                TestSample.Outbound(false, false, false,    0, TimeSpan.FromSeconds(260),       IPAddress.Parse("2000:0db8:85a3:1232:0000:8a2e:0370:7334")), // IP address already used for outbound.
                TestSample.Outbound(true,  false, false,    0, TimeSpan.FromSeconds(-2126.0),   new IPAddress(2)),
                TestSample.Inbound(true,   false, false,    0, TimeSpan.FromSeconds(-391),      new IPAddress(3)),                           

                // These samples will change adjusted time because next outbound is the 4th outbound and we are under the limits.
                TestSample.Outbound(true,  false, false, 1280, TimeSpan.FromSeconds(-1),        new IPAddress(4)),  // 2 inbound, 4 outbound. 2/4 * 3 = 1.5 -> ceil -> 2 of each outbound   { -2126000, -2126000, -391000, -1000, -1000, 3560, 3560, 13123, 26000, 26000 } -> median is 1280ms.
                TestSample.Inbound(true,   false, false, 3560, TimeSpan.FromSeconds(23.6),      new IPAddress(5)),  // 3 inbound, 4 outbound. 3/4 * 3 = 2.25 -> ceil -> 3 of each outbound  { -2126000, -2126000, -2126000, -391000, -1000, -1000, -1000, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000 } -> median is 3560ms.                     
                TestSample.Inbound(true,   false, false, 3560, TimeSpan.FromSeconds(236),       new IPAddress(6)),  // 4 inbound, 4 outbound. 4/4 * 3 = 3 -> ceil -> 3 of each outbound     { -2126000, -2126000, -2126000, -391000, -1000, -1000, -1000, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 } -> median is 3560ms.                                                       
                TestSample.Outbound(true,  false, false, 1236, TimeSpan.FromSeconds(1.236),     new IPAddress(7)),  // 4 inbound, 5 outbound. 4/5 * 3 = 2.4 -> ceil -> 3 of each outbound   { -2126000, -2126000, -2126000, -391000, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }  -> median is 1236ms.
                TestSample.Inbound(true,   false, false, 1236, TimeSpan.FromSeconds(-1001),     new IPAddress(8)),  // 5 inbound, 5 outbound. 5/5 * 3 = 3 -> ceil -> 3 of each outbound     { -2126000, -2126000, -2126000, -391000, -1001, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }  -> median is 1236ms.
                TestSample.Inbound(true,   false, false, 1236, TimeSpan.FromSeconds(-4.9236),   new IPAddress(9)),  // 6 inbound, 5 outbound. 6/5 * 3 = 3.6 -> ceil -> 4 of each outbound   { -2126000, -2126000, -2126000, -2126000, -391000, -4923.6, -1001, -1000, -1000, -1000, -1000, 1236, 1236, 1236, 1236, 3560, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 26000, 236000 }  -> median is 1236ms.
                TestSample.Outbound(true,  false, false,  118, TimeSpan.FromSeconds(-4444.444), new IPAddress(10)), // 6 inbound, 6 outbound. 6/6 * 3 = 3 -> ceil -> 3 of each outbound    { -4444444, -4444444, -4444444, -2126000, -2126000, -2126000, -391000, -4923.6, -1001, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }  -> median is 118ms.
            };

            int maliciousOffset = TimeSyncBehaviorState.MaxTimeOffsetSeconds - 1;

            // Introduce 100 malicious inbound and show we are protected from malicious inbounds.
            for (int i = 11; i < 111; i++)
            {
                // Median always lands on one of the outbounds no matter how many malicious inbounds.
                samples.Add(TestSample.Inbound(true, false, false, 1236, TimeSpan.FromSeconds(maliciousOffset), new IPAddress(i)));
            }

            // Add 3 malicious outbound which is 3/9 (33.3%) which is equal to the 33.3% of outbounds and show protected.
            // Median always lands on one of the non malicious outbound.
            samples.Add(TestSample.Outbound(true, false, false, 3560, TimeSpan.FromSeconds(maliciousOffset), new IPAddress(11)));
            samples.Add(TestSample.Outbound(true, false, false, 26000, TimeSpan.FromSeconds(maliciousOffset), new IPAddress(12)));
            samples.Add(TestSample.Outbound(true, false, false, 26000, TimeSpan.FromSeconds(maliciousOffset), new IPAddress(13)));

            // Add a 4th malicious outbound which is 4/10 (40%) which is greater that 33.3% of outbounds. Show we are not protected -> the offset gets set to the malicious value.
            samples.Add(TestSample.Outbound(true, false, false, maliciousOffset * 1000, TimeSpan.FromSeconds(maliciousOffset), new IPAddress(14)));

            var dateTimeProvider = new DateTimeProvider();
            TimeSyncBehaviorState state = this.CreateTimeSyncBehaviorState(dateTimeProvider);

            for (int i = 0; i < samples.Count; i++)
            {
                bool used = state.AddTimeData(samples[i].PeerIpAddress, samples[i].InputTimeOffset, samples[i].IsInbound);
                Assert.Equal(samples[i].ExpectedIsUsed, used);

                Assert.Equal(samples[i].ExpectedIsSyncOff, state.SwitchedOffLimitReached);
                Assert.Equal(samples[i].ExpectedIsSyncOff, state.SwitchedOff);

                DateTime adjustedTime = dateTimeProvider.GetAdjustedTime();
                DateTime normalTime = dateTimeProvider.GetUtcNow();
                TimeSpan diff = adjustedTime - normalTime;

                Assert.True(Math.Abs(diff.TotalMilliseconds - samples[i].ExpectedTimeOffsetMs) < TimeEpsilonMs, $"Failed in sample at index: {i}. Actual offset milliseconds: {diff.TotalMilliseconds}. Expected offset milliseconds: {samples[i].ExpectedTimeOffsetMs}");
            }
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// turns on the user warnings and then switches off the time sync feature on defined threshold levels.
        /// </summary>
        [Fact]
        public void AddTimeData_WithSmallSampleSet_TurnsWarningOnAndSwitchesSyncOff()
        {
            int offsetAboveWarningLevelSeconds = TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 1;
            int offsetAboveWarningLevelMs = offsetAboveWarningLevelSeconds * 1000;

            int offsetAbovSwitchOffLevel = BitcoinMain.BitcoinMaxTimeOffsetSeconds + 1;
            int offsetAbovSwitchOffLevelMs = offsetAbovSwitchOffLevel * 1000;

            // Samples to be inserted to the state.
            // Columns meanings:  isUsed, isWarningOn, isSyncOff, timeOffsetSample, peerAddress
            var samples = new List<TestSample>
            {
                // First group of samples does not affect adjusted time, so difference should be ~0 ms.
                TestSample.Outbound(true,  false, false, 0,                             TimeSpan.FromSeconds(offsetAboveWarningLevelSeconds), IPAddress.Parse("1.2.3.41")),
                TestSample.Outbound(true,  false, false, 0,                             TimeSpan.FromSeconds(offsetAboveWarningLevelSeconds), IPAddress.Parse("1.2.3.42")),
                TestSample.Outbound(true,  false, false, 0,                             TimeSpan.FromSeconds(offsetAboveWarningLevelSeconds), IPAddress.Parse("1.2.3.43")), 
                                                                                        
                // The next sample turns on the warning.                                
                TestSample.Outbound(true,  true,  false, offsetAboveWarningLevelMs,     TimeSpan.FromSeconds(offsetAboveWarningLevelSeconds), IPAddress.Parse("1.2.3.44")), 
                                                                                        
                // It can't be turned off.                                              
                TestSample.Outbound(true,  true,  false, offsetAboveWarningLevelMs,     TimeSpan.FromSeconds(0),                              IPAddress.Parse("1.2.3.45")),
                TestSample.Outbound(true,  true,  false, offsetAboveWarningLevelMs,     TimeSpan.FromSeconds(0),                              IPAddress.Parse("1.2.3.46")), 
                                                                                        
                // Add more samples (above switch off level, trying to switch it off).  
                TestSample.Outbound(true,  true,  false, offsetAboveWarningLevelMs,     TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.3.47")),
                TestSample.Outbound(true,  true,  false, offsetAboveWarningLevelMs/2,   TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.3.48")),
                TestSample.Outbound(true,  true,  false, 0,                             TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.3.49")),
                TestSample.Outbound(true,  true,  false, 0,                             TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.31.4")),
                TestSample.Outbound(true,  true,  false, 0,                             TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.32.4")),
                TestSample.Outbound(true,  true,  false, -offsetAbovSwitchOffLevelMs/2, TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.33.4")), 

                // Now the feature should be turned off.
                TestSample.Outbound( true,  true,  true, 0,                             TimeSpan.FromSeconds(-offsetAbovSwitchOffLevel),      IPAddress.Parse("1.2.33.5")), 
                                                                                                                                              
                // No more samples should be accepted now.                                                                                    
                TestSample.Outbound(false, true,  true,  0,                             TimeSpan.FromSeconds(2),                              IPAddress.Parse("1.2.34.4")),
                TestSample.Outbound(false, true,  true,  0,                             TimeSpan.FromSeconds(1),                              IPAddress.Parse("1.2.35.4")),
            };

            var dateTimeProvider = new DateTimeProvider();
            TimeSyncBehaviorState state = this.CreateTimeSyncBehaviorState(dateTimeProvider);


            for (int i = 0; i < samples.Count; i++)
            {
                bool used = state.AddTimeData(samples[i].PeerIpAddress, samples[i].InputTimeOffset, samples[i].IsInbound);

                Assert.Equal(samples[i].ExpectedIsUsed, used);
                Assert.Equal(samples[i].ExpectedIsWarningOn, state.IsSystemTimeOutOfSync);
                Assert.Equal(samples[i].ExpectedIsSyncOff, state.SwitchedOffLimitReached);
                Assert.Equal(samples[i].ExpectedIsSyncOff, state.SwitchedOff);

                TimeSpan diff = dateTimeProvider.GetAdjustedTime() - dateTimeProvider.GetUtcNow();
                Assert.True(Math.Abs(diff.TotalMilliseconds - samples[i].ExpectedTimeOffsetMs) < TimeEpsilonMs, $"Failed in sample at index: {i}. Actual offset milliseconds: {diff.TotalMilliseconds}. Expected offset milliseconds: {samples[i].ExpectedTimeOffsetMs}");
            }
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// forgets old samples if it has reached predefined limits.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeOutboundSampleSet_ForgetsOldSamples()
        {
            var dateTimeProvider = new DateTimeProvider();
            TimeSyncBehaviorState state = this.CreateTimeSyncBehaviorState(dateTimeProvider);

            // Add max outbound.
            for (int i = 0; i < TimeSyncBehaviorState.MaxOutboundSamples; i++)
            {
                var peerAddress = new IPAddress(i);
                bool used = state.AddTimeData(peerAddress, TimeSpan.FromSeconds(400), false);
                Assert.True(used);
            }

            TimeSpan diff = dateTimeProvider.GetAdjustedTime() - dateTimeProvider.GetUtcNow();
            Assert.True(Math.Abs(diff.TotalMilliseconds - 400 * 1000) < TimeEpsilonMs, $"should be 400ms because of outbound preference. Actual: {diff.TotalMilliseconds}ms");

            // Add another batch of outbounds with a different offset.
            for (int i = TimeSyncBehaviorState.MaxOutboundSamples; i < TimeSyncBehaviorState.MaxOutboundSamples * 2; i++)
            {
                var peerAddress = new IPAddress(i * 2);
                bool used = state.AddTimeData(peerAddress, TimeSpan.FromSeconds(800), false);
                Assert.True(used, $"index: {i}");
            }

            diff = dateTimeProvider.GetAdjustedTime() - dateTimeProvider.GetUtcNow();
            Assert.True(Math.Abs(diff.TotalMilliseconds - 800 * 1000) < TimeEpsilonMs, $"should be 800ms because of outbound preference. Actual: {diff.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// forgets old samples if it has reached predefined limits.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeInboundSampleSet_ForgetsOldSamples()
        {
            var dateTimeProvider = new DateTimeProvider();
            TimeSyncBehaviorState state = this.CreateTimeSyncBehaviorState(dateTimeProvider);

            // Add max inbound samples.
            for (int i = 0; i < TimeSyncBehaviorState.MaxInboundSamples; i++)
            {
                bool used = state.AddTimeData(new IPAddress(i), TimeSpan.FromSeconds(400), true);
                Assert.True(used);
            }

            // Add enough outbound samples to get an adjustment (and across an even range).
            Assert.True(state.AddTimeData(new IPAddress(1), TimeSpan.FromSeconds(-500), false));
            Assert.True(state.AddTimeData(new IPAddress(2), TimeSpan.FromSeconds(-250), false));
            Assert.True(state.AddTimeData(new IPAddress(3), TimeSpan.FromSeconds(250), false));
            Assert.True(state.AddTimeData(new IPAddress(4), TimeSpan.FromSeconds(500), false));

            TimeSpan diff = dateTimeProvider.GetAdjustedTime() - dateTimeProvider.GetUtcNow();
            Assert.True(Math.Abs(diff.TotalMilliseconds - 250 * 1000) < TimeEpsilonMs, $"Should be 250ms because leaning towards the inbound value of 400ms, but still in outbound. Actual: {diff.TotalMilliseconds}ms");

            // Add another batch of inbound samples with a greater offset than highest outbound sample.
            for (int i = TimeSyncBehaviorState.MaxInboundSamples; i < TimeSyncBehaviorState.MaxInboundSamples * 2; i++)
            {
                Assert.True(state.AddTimeData(new IPAddress(i * 2), TimeSpan.FromSeconds(-1000), true), $"index: {i}");
            }

            diff = dateTimeProvider.GetAdjustedTime() - dateTimeProvider.GetUtcNow();
            Assert.True(Math.Abs(diff.TotalMilliseconds - (-250 * 1000)) < TimeEpsilonMs, $"Should be -250ms because the new inbound samples replace the old and swing the median closer to the new -1000ms values, but still in outbound. Actual: {diff.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Sets up the system under test.
        /// </summary>
        /// <param name="dateTimeProvider">The datetime provider injected to the constructor of the system under test.</param>
        /// <returns>The system under test.</returns>
        private TimeSyncBehaviorState CreateTimeSyncBehaviorState(IDateTimeProvider dateTimeProvider)
        {
            var lifetime = new NodeLifetime();
            var loggerFactory = new LoggerFactory();
            var asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            var state = new TimeSyncBehaviorState(dateTimeProvider, lifetime, asyncLoopFactory, loggerFactory, KnownNetworks.Main);
            return state;
        }

        /// <summary>
        /// Representation of a Sample for data driven tests.
        /// </summary>
        public class TestSample
        {
            /// <summary>
            /// Input isInbound flag, with false meaning outbound.
            /// </summary>
            public bool IsInbound { get; }

            /// <summary>
            ///Expectation of whether the sample is used or not. In many cases samples are either included or not and the AddTimeData method returns this information as a bool.
            /// </summary>
            public bool ExpectedIsUsed { get; }


            /// <summary>
            /// >Expectation of whether the warning has been set to on.
            /// </summary>
            public bool ExpectedIsWarningOn { get; }

            /// <summary>
            /// Expectation of whether the sync has been set to off.
            /// </summary>
            public bool ExpectedIsSyncOff { get; }

            /// <summary>
            /// Expected value of the time offset after this sample is processed.
            /// </summary>
            public int ExpectedTimeOffsetMs { get; }

            /// <summary>
            /// The actual input time offset from the sample.
            /// </summary>
            public TimeSpan InputTimeOffset { get; }

            /// <summary>
            /// The IP Address of the sample.
            /// </summary>
            public IPAddress PeerIpAddress { get; }

            /// <summary>
            /// Private constructor, forcing the factory methods Inbound and Outbound to be used.
            /// </summary>
            /// <param name="isInbound"><c>true</c> if the instance represents an inbound sample, <c>false</c> if it represents and outbound sample.</param>
            /// <param name="expectedIsUsed">Expectation of whether the sample is used or not. In many cases samples are either included or not and the AddTimeData method returns this information as a bool.</param>
            /// <param name="expectedIsWarningOn">Expectation of whether the warning has been set to on.</param>
            /// <param name="expectedIsSyncOff">Expectation of whether the sync has been set to off.</param>
            /// <param name="expectedTimeOffsetMs">Expected value of the time offset after this sample is processed.</param>
            /// <param name="inputTimeOffset">The actual input time offset from the sample.</param>
            /// <param name="peerIpAddress">The IP Address of the sample.</param>
            private TestSample(bool isInbound, bool expectedIsUsed, bool expectedIsWarningOn, bool expectedIsSyncOff, int expectedTimeOffsetMs, TimeSpan inputTimeOffset, IPAddress peerIpAddress)
            {
                this.IsInbound = isInbound;
                this.ExpectedIsUsed = expectedIsUsed;
                this.ExpectedIsWarningOn = expectedIsWarningOn;
                this.ExpectedIsSyncOff = expectedIsSyncOff;
                this.ExpectedTimeOffsetMs = expectedTimeOffsetMs;
                this.InputTimeOffset = inputTimeOffset;
                this.PeerIpAddress = peerIpAddress;
            }

            public static TestSample Inbound(bool expectedIsUsedResult, bool expectedIsWarningOn, bool expectedIsSyncOff, int expectedTimeOffsetLessThanMs, TimeSpan inputTimeOffset, IPAddress ipAddress)
            {
                return new TestSample(true, expectedIsUsedResult, expectedIsWarningOn, expectedIsSyncOff, expectedTimeOffsetLessThanMs, inputTimeOffset, ipAddress);
            }

            public static TestSample Outbound(bool expectedIsUsedResult, bool expectedIsWarningOn, bool expectedIsSyncOff, int expectedTimeOffsetMs, TimeSpan inputTimeOffset, IPAddress ipAddress)
            {
                return new TestSample(false, expectedIsUsedResult, expectedIsWarningOn, expectedIsSyncOff, expectedTimeOffsetMs, inputTimeOffset, ipAddress);
            }
        }
    }
}