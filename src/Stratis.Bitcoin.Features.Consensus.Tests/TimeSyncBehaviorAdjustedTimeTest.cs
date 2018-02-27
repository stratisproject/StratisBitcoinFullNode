using System;
using System.Net;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Tests of <see cref="TimeSyncBehavior"/> and <see cref="TimeSyncBehaviorState"/> classes.
    /// </summary>
    public partial class TimeSyncBehaviorAdjustedTimeTest
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
        /// This is the result of the operation under test. It is only rounded so that assertions are easier, a future improvement would be to not use UtcNow directly in the code, allowing it to be replaced at test time.
        /// </summary>
        private int roundedOffset;

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// uses outbound samples as a priority over inbound as they are less likely to be malicious.
        /// This means that even when there are many inbound connections, only the first ones are considered until there are enough outbound nodes. 
        /// Eg only 9 inbound per 1 outbound.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSetOfInboundTimeManipulatorsAndLowSampleSetOfOutbound_GetsOverridenByOutboundSamples()
        {
            given_an_empty_time_sync_behaviour_state();
            given_40_inbound_samples_with_offset_of(10);
            given_1_outbound_sample_with_offset_of(20);
            when_calculating_time_adjust_offset_to_nearest_second();
            then_adjusted_time_offset_should_be(20);
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// uses system time without an offset when there are no outbound samples.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSetOfInboundTimeManipulatorsAndZeroOutbound_SticksWithTheSystemTime()
        {
            given_an_empty_time_sync_behaviour_state();
            given_40_inbound_samples_with_offset_of(10);
            given_0_outbound_samples();
            when_calculating_time_adjust_offset_to_nearest_second();
            then_adjusted_time_offset_should_be(0);
        }

        private void given_an_empty_time_sync_behaviour_state()
        {
            this.dateTimeProvider = new DateTimeProvider();
            this.timesyncBehaviourState = new TimeSyncBehaviorState(
                this.dateTimeProvider, new NodeLifetime(),
                new AsyncLoopFactory(new LoggerFactory()), 
                new LoggerFactory());
        }

        private void given_1_outbound_sample_with_offset_of(int offsetSeconds)
        {
            this.timesyncBehaviourState.AddTimeData(IPAddress.Parse("2.2.2.2"), TimeSpan.FromSeconds(offsetSeconds), isInboundConnection: false);
        }

        private void given_0_outbound_samples()
        {
        }

        private void given_40_inbound_samples_with_offset_of(int offset)
        {
            for (int i = 1; i <= 40; i++)
            {
                this.timesyncBehaviourState.AddTimeData(IPAddress.Parse("1.2.3." + i), TimeSpan.FromSeconds(offset), isInboundConnection: true);
            }
        }

        private void when_calculating_time_adjust_offset_to_nearest_second()
        {
            var adjustedTime = this.dateTimeProvider.GetAdjustedTime();
            var now = this.dateTimeProvider.GetUtcNow();
            var offset = adjustedTime - now;

            this.roundedOffset = (int)Math.Round((decimal) offset.TotalMilliseconds, MidpointRounding.AwayFromZero) / 1000;
        }

        private void then_adjusted_time_offset_should_be(int expectedOffset)
        {
            Assert.Equal(expectedOffset, this.roundedOffset);
        }
    }
}