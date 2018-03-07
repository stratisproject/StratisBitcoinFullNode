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
            var samples = new List<TestSample>
            {
                // Columns meanings: expectedIsUsed, expectedIsWarningOn, expectedIsTimeSyncOff, expectedTimeOffsetLessThanMs, timeOffsetSample, peerAddress

                // First group of samples does not affect adjusted time, so difference should be ~0 ms.
                TestSample.Outbound(true,  false, false,    0, TimeSpan.FromSeconds(3.56),      IPAddress.Parse("1.2.3.4")),
                TestSample.Inbound(true,   false, false,    0, TimeSpan.FromSeconds(13.123),    IPAddress.Parse("1.2.3.4")),
                TestSample.Outbound(false, false, false,    0, TimeSpan.FromSeconds(7.123),     IPAddress.Parse("1.2.3.4")), // ip address already used.
                TestSample.Outbound(true,  false, false,    0, TimeSpan.FromSeconds(26.0),      IPAddress.Parse("2001:0db8:85a3:1232:0000:8a2e:0370:7334")),
                TestSample.Outbound(false, false, false,    0, TimeSpan.FromSeconds(260),       IPAddress.Parse("2001:0db8:85a3:1232:0000:8a2e:0370:7334")), // ip address already used.
                TestSample.Outbound(true,  false, false,    0, TimeSpan.FromSeconds(-2126.0),   IPAddress.Parse("1.2.3.5")),                                
                TestSample.Inbound( true,  false, false,    0, TimeSpan.FromSeconds(-391),      IPAddress.Parse("1.2.3.45")),                                

                // These samples will change adjusted time because next outbound is the 4th outbound.
               
                TestSample.Outbound(true,  false, false, 1280, TimeSpan.FromSeconds(-1),        IPAddress.Parse("2.2.2.2")),  // 2 inbound, 4 outbound. 2/4 * 3 = 1.5 -> ceil -> 2 of each outbound   { -2126000, -2126000, -391000, -1000, -1000, 3560, 3560, 13123, 26000, 26000 } -> median is 1280ms
                TestSample.Inbound( true,  false, false, 3560, TimeSpan.FromSeconds(23.6),      IPAddress.Parse("::1")),      // 3 inbound, 4 outbound. 3/4 * 3 = 2.25 -> ceil -> 3 of each outbound  { -2126000, -2126000, -2126000, -391000, -1000, -1000, -1000, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000 } -> median is 3560ms                                
                TestSample.Inbound( true,  false, false, 3560, TimeSpan.FromSeconds(236),       IPAddress.Parse("12.2.3.5")), // 4 inbound, 4 outbound. 4/4 * 3 = 3 -> ceil -> 3 of each outbound     { -2126000, -2126000, -2126000, -391000, -1000, -1000, -1000, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 } -> median is 3560ms                                                           
                TestSample.Outbound(true,  false, false, 1236, TimeSpan.FromSeconds(1.236),     IPAddress.Parse("13.2.3.5")), // 4 inbound, 5 outbound. 4/5 * 3 = 2.4 -> ceil -> 3 of each outbound   { -2126000, -2126000, -2126000, -391000, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }  -> median is 1236ms                            
                TestSample.Inbound( true,  false, false, 1236, TimeSpan.FromSeconds(-1001),     IPAddress.Parse("14.2.3.5")), // 5 inbound, 5 outbound. 5/5 * 3 = 3 -> ceil -> 3 of each outbound     { -2126000, -2126000, -2126000, -391000, -1001, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }  -> median is 1236ms                                                       
                TestSample.Inbound( true,  false, false, 1236, TimeSpan.FromSeconds(-4.9236),   IPAddress.Parse("15.2.3.5")), // 6 inbound, 5 outbound. 6/5 * 3 = 3.6 -> ceil -> 4 of each outbound   { -2126000, -2126000, -2126000, -2126000, -391000, -4923.6, -1001, -1000, -1000, -1000, -1000, 1236, 1236, 1236, 1236, 3560, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 26000, 236000 }  -> median is 1236ms                                               
                TestSample.Outbound(true,  false, false,  118, TimeSpan.FromSeconds(-4444.444), IPAddress.Parse("16.2.3.5")), // 6 inbound, 6 outbound. 6/6 * 3 = 3 -> ceil -> 3 of each outbound     { -4444444, -4444444, -4444444, -2126000, -2126000, -2126000, -391000, -4923.6, -1001, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }  -> median is 118ms                              

                // add 10 inbound  16/6
            };

            var x = new[]  { -4444444, -4444444, -4444444, -2126000, -2126000, -2126000, -391000, -4923.6, -1001, -1000, -1000, -1000, 1236, 1236, 1236, 3560, 3560, 3560, 13123, 23600, 26000, 26000, 26000, 236000 }.Median();
            Assert.Equal(0, x);

            var dateTimeProvider = DateTimeProvider.Default;
            var lifetime = new NodeLifetime();
            var loggerFactory = new LoggerFactory();
            var asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            var state = new TimeSyncBehaviorState(dateTimeProvider, lifetime, asyncLoopFactory, loggerFactory);

            for (int i = 0; i < samples.Count; i++)
            {
                bool isInbound = samples[i].IsInbound;
                bool isUsed = samples[i].ExpectedIsUsedResult;
                bool isWarningOn = samples[i].ExpectedIsWarningOn;
                bool isSyncOff = samples[i].ExpectedIsSyncOff;
                int expectedTimeOffsetLessThanMs = samples[i].ExpectedTimeOffsetLessThanMs;
                TimeSpan timeOffsetSample = samples[i].InputTimeOffset;
                IPAddress peerAddress = samples[i].PeerIpAddress;

                //Assert.Equal(isWarningOn, state.IsSystemTimeOutOfSync);
                //Assert.Equal(isSyncOff, state.SwitchedOffLimitReached);
                //Assert.Equal(isSyncOff, state.SwitchedOff);

                bool used = state.AddTimeData(peerAddress, timeOffsetSample, isInbound);
                Assert.Equal(isUsed, used);

                DateTime adjustedTime = dateTimeProvider.GetAdjustedTime();
                DateTime normalTime = dateTimeProvider.GetUtcNow();
                TimeSpan diff = adjustedTime - normalTime;
                  
                Assert.True(Math.Abs(diff.TotalMilliseconds) < expectedTimeOffsetLessThanMs + TimeEpsilonMs, $"Failed in sample at index: {i}. Actual offset milliseconds: {diff.TotalMilliseconds}. Expected offset milliseconds: {expectedTimeOffsetLessThanMs}");
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
            // Columns meanings:  isUsed, isWarningOn, isSyncOff, timeOffsetSample, peerAddress
            var samples = new List<TestSample>
            {
                // First group of samples does not affect adjusted time, so difference should be ~0 ms.
                TestSample.Outbound(true,  false, false, 0, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 1), IPAddress.Parse("1.2.3.41")), 
                TestSample.Outbound(true,  false, false, 0, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 2), IPAddress.Parse("1.2.3.42")), 
                TestSample.Outbound(true,  false, false, 0, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 3), IPAddress.Parse("1.2.3.43")), 

                // The next sample turns on the warning.
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(TimeSyncBehaviorState.TimeOffsetWarningThresholdSeconds + 4), IPAddress.Parse("1.2.3.44")), 

                // It can't be turned off.
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(3),                                                           IPAddress.Parse("1.2.3.45")), 
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(4),                                                           IPAddress.Parse("1.2.3.46")), 

                // Add more samples.
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 10),            IPAddress.Parse("1.2.3.47")), 
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 11),            IPAddress.Parse("1.2.3.48")), 
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 12),            IPAddress.Parse("1.2.3.49")), 
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 13),            IPAddress.Parse("1.2.31.4")), 
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 14),            IPAddress.Parse("1.2.32.4")), 
                TestSample.Outbound(true,  true,  false, 0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 15),            IPAddress.Parse("1.2.33.4")), 

                // Now the feature should be turned off.
                TestSample.Inbound( true,  true,  true,  0, TimeSpan.FromSeconds(-TimeSyncBehaviorState.MaxTimeOffsetSeconds - 16),            IPAddress.Parse("1.2.33.4")), 

                // No more samples should be accepted now.
                TestSample.Outbound(false, true,  true,  0, TimeSpan.FromSeconds(2),                                                           IPAddress.Parse("1.2.34.4")), 
                TestSample.Outbound(false, true,  true,  0, TimeSpan.FromSeconds(1),                                                           IPAddress.Parse("1.2.35.4")),  
            };

            var dateTimeProvider = DateTimeProvider.Default;
            var lifetime = new NodeLifetime();
            var loggerFactory = new LoggerFactory();
            var asyncLoopFactory = new AsyncLoopFactory(loggerFactory);
            var state = new TimeSyncBehaviorState(dateTimeProvider, lifetime, asyncLoopFactory, loggerFactory);

            for (int i = 0; i < samples.Count; i++)
            {
                bool isInbound = samples[i].IsInbound;
                bool isUsed = samples[i].ExpectedIsUsedResult;
                bool isWarningOn = samples[i].ExpectedIsWarningOn;
                bool isSyncOff = samples[i].ExpectedIsSyncOff;
                TimeSpan timeOffsetSample = samples[i].InputTimeOffset;
                IPAddress peerAddress = samples[i].PeerIpAddress;

                bool used = state.AddTimeData(peerAddress, timeOffsetSample, isInbound);
                Assert.Equal(isUsed, used);
                 
                Assert.Equal(isWarningOn, state.IsSystemTimeOutOfSync);
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
            int inboundSamples = 300; //100
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
            Assert.True(Math.Abs(diff.TotalMilliseconds) - Math.Abs(expectedDiffMs) < TimeEpsilonMs);

            var outSamples = new List<int>();
            for (int i = 0; i < outboundSamples; i++)
            {
                IPAddress peerAddress = new IPAddress(i);
                bool used = state.AddTimeData(peerAddress, TimeSpan.FromSeconds(-i), false);
                Assert.True(used);
                if (i >= outboundSamples - TimeSyncBehaviorState.MaxOutboundSamples) outSamples.Add(-i * 1000);
            }

            var allSamples = new List<int>(inSamples);
            for (int i = 0; i < TimeSyncBehaviorState.OffsetWeightSecurityConstant; i++)
                allSamples.AddRange(outSamples);

            adjustedTime = dateTimeProvider.GetAdjustedTime();
            normalTime = dateTimeProvider.GetUtcNow();
            diff = adjustedTime - normalTime;
            expectedDiffMs = allSamples.Median();
            Assert.True(Math.Abs(diff.TotalMilliseconds) < Math.Abs(expectedDiffMs) + TimeEpsilonMs);
        }
    }

    /// <summary>
    /// Representation of a Sample for data driven tests.
    /// </summary>
    public class TestSample
    {
        public bool IsInbound { get; }
        public bool ExpectedIsUsedResult { get; }
        public bool ExpectedIsWarningOn { get; }
        public bool ExpectedIsSyncOff { get; }
        public int ExpectedTimeOffsetLessThanMs { get; }
        public TimeSpan InputTimeOffset { get; }
        public IPAddress PeerIpAddress { get; }

        /// <summary>
        /// Private constructor, forcing the factory methods Inbound and Outbound to be used.
        /// </summary>
        /// <param name="isInbound">Input isInbound flag, with false meaning outbound.</param>
        /// <param name="expectedIsUsedResult">Expectation of whether the sample is used or not.</param>
        /// <param name="expectedIsWarningOn">Expectation of whether the warning has been set to on.</param>
        /// <param name="expectedIsSyncOff">Expectation of whether the sync has been set to off.</param>
        /// <param name="expectedTimeOffsetLessThanMs">Expectation that the time offset will be less than this value.</param>
        /// <param name="inputTimeOffset">The actual inout time offset from the sample.</param>
        /// <param name="peerIpAddress">The IP Address of the sample.</param>
        private TestSample(bool isInbound, bool expectedIsUsedResult, bool expectedIsWarningOn, bool expectedIsSyncOff, int expectedTimeOffsetLessThanMs, TimeSpan inputTimeOffset, IPAddress peerIpAddress)
        {
            this.IsInbound = isInbound;
            this.ExpectedIsUsedResult = expectedIsUsedResult;
            this.ExpectedIsWarningOn = expectedIsWarningOn;
            this.ExpectedIsSyncOff = expectedIsSyncOff;
            this.ExpectedTimeOffsetLessThanMs = expectedTimeOffsetLessThanMs;
            this.InputTimeOffset = inputTimeOffset;
            this.PeerIpAddress = peerIpAddress;
        }
        
        public static TestSample Inbound(bool expectedIsUsedResult, bool expectedIsWarningOn, bool expectedIsSyncOff, int expectedTimeOffsetLessThanMs, TimeSpan inputTimeOffset, IPAddress ipAddress)
        {
            return new TestSample(true, expectedIsUsedResult, expectedIsWarningOn, expectedIsSyncOff, expectedTimeOffsetLessThanMs, inputTimeOffset, ipAddress);
        }

        public static TestSample Outbound(bool expectedIsUsedResult, bool expectedIsWarningOn,  bool expectedIsSyncOff, int expectedTimeOffsetLessThanMs, TimeSpan inputTimeOffset, IPAddress ipAddress)
        {
            return new TestSample(false, expectedIsUsedResult, expectedIsWarningOn, expectedIsSyncOff, expectedTimeOffsetLessThanMs, inputTimeOffset, ipAddress);
        }
    }
}