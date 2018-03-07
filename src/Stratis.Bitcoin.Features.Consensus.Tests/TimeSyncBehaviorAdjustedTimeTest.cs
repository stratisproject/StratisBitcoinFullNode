using System;
using System.Net;
using Microsoft.AspNetCore.JsonPatch.Internal;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Tests of <see cref="TimeSyncBehavior"/> and <see cref="TimeSyncBehaviorState"/> classes.
    /// </summary>
    public class TimeSyncBehaviorAdjustedTimeTest
    {
        /// <summary>
        /// Time data is added this state. 
        /// </summary>
        private TimeSyncBehaviorState timesyncBehaviourState;

        /// <summary>
        /// This provides the adjusted time ready for assertion. 
        /// </summary>
        private IDateTimeProvider dateTimeProvider;
        
        /// <summary>
        /// This is the result of the <see cref="IDateTimeProvider.GetAdjustedTime"/>. 
        /// </summary>
        private TimeSpan adjustedOffsetTimespan;

        /// <summary>
        /// Mocked out logger for assertion.
        /// </summary>
        private Mock<ILogger> logger;

        /// <summary>
        /// Generates IP Addresses from a starting point
        /// </summary>
        private IPAddressGenerator ipAddressGenerator;

        public TimeSyncBehaviorAdjustedTimeTest()
        {
            this.ipAddressGenerator = new IPAddressGenerator(IPAddress.Parse("1.1.1.1"));
        }
        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// uses outbound samples as a priority over inbound as they are less likely to be malicious.
        /// This means that even when there are many inbound connections, 
        /// only the first (<see cref="TimeSyncBehaviorState.OffsetWeightSecurityConstant"/> -1) inbound per outbound are considered. 
        /// Eg only 9 inbound per 1 outbound if the ratio is 10:1 inbound:outbound.
        /// <remarks>An adjusted offset is also only considered after 40 samples, hence 40 inbound used in the test.</remarks>
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSetOfInboundTimeManipulatorsAndLowSampleSetOfOutbound_GetsOverridenByOutboundSamples()
        {
            this.Given_an_empty_time_sync_behaviour_state();
            this.Given_40_inbound_samples_with_offset_of(10);
            this.Given_4_outbound_samples_with_offset_of(20);
            this.When_calculating_time_adjust_offset();
            this.Then_adjusted_time_offset_is(20);
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// uses system time without an offset when there are no outbound samples.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSetOfInboundTimeManipulatorsAndZeroOutbound_SticksWithTheSystemTime()
        {
            this.Given_an_empty_time_sync_behaviour_state();
            this.Given_40_inbound_samples_with_offset_of(10);
            this.When_calculating_time_adjust_offset();
            this.Then_adjusted_time_offset_is(0);
        }

        [Fact]
        public void AddTimeData_WithLargeInboundMaliciousAnd33PercentOutboundMalicious_StillProtected()
        {
            this.Given_an_empty_time_sync_behaviour_state();
            this.Given_40_inbound_samples_with_offset_of(-100);
            this.Given_1_outbound_samples_with_offset_of(-100);
            this.Given_3_outbound_samples_with_offset_of(20);
            this.When_calculating_time_adjust_offset();
            this.Then_adjusted_time_offset_is(20);
        }

        [Fact]
        public void AddTimeData_WithLargeInboundMaliciousAnd34PercentOutboundMalicious_NotProtected()
        {
            throw new Exception();
        }

        private void Given_an_empty_time_sync_behaviour_state()
        {
            this.dateTimeProvider = new DateTimeProvider();
            var loggerFactory = new Mock<ILoggerFactory>();
            this.logger = new Mock<ILogger>();

            loggerFactory
                .Setup(x => x.CreateLogger(It.IsAny<string>()))
                .Returns(this.logger.Object);

            this.timesyncBehaviourState = new TimeSyncBehaviorState(
                this.dateTimeProvider, new NodeLifetime(),
                new AsyncLoopFactory(loggerFactory.Object), 
                loggerFactory.Object);
        }

        private void Given_1_outbound_samples_with_offset_of(int offsetSeconds)
        {
            this.timesyncBehaviourState.AddTimeData(this.ipAddressGenerator.GetNext(), TimeSpan.FromSeconds(offsetSeconds), isInboundConnection: false);
        }

        private void Given_3_outbound_samples_with_offset_of(int offsetSeconds)
        {
            for (int i = 0; i < 3; i++)
            {
                this.timesyncBehaviourState.AddTimeData(this.ipAddressGenerator.GetNext(), TimeSpan.FromSeconds(offsetSeconds), isInboundConnection: false);
            }
        }

        private void Given_4_outbound_samples_with_offset_of(int offsetSeconds)
        {
            for (int i = 0; i < 4; i++)
            {
                this.timesyncBehaviourState.AddTimeData(this.ipAddressGenerator.GetNext(), TimeSpan.FromSeconds(offsetSeconds), isInboundConnection: false);
            }
        }

        private void Given_40_inbound_samples_with_offset_of(int offsetSeconds)
        {
            for (int i = 1; i <= 40; i++)
            {
                this.timesyncBehaviourState.AddTimeData(this.ipAddressGenerator.GetNext(), TimeSpan.FromSeconds(offsetSeconds), isInboundConnection: true);
            }
        }

        private void When_calculating_time_adjust_offset()
        {
            this.adjustedOffsetTimespan = this.dateTimeProvider.GetAdjustedTime() - this.dateTimeProvider.GetUtcNow();
        }

        private void Then_adjusted_time_offset_is(int expectedOffsetInSeconds)
        {
            int roundedOffset = (int) Math.Round((decimal) this.adjustedOffsetTimespan.TotalMilliseconds,
                                    MidpointRounding.AwayFromZero) / 1000;
             
            Assert.Equal(expectedOffsetInSeconds, roundedOffset);
        }
    }
}