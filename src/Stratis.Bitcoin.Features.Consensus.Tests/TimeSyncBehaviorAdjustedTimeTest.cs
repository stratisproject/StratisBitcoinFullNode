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
        /// This is the result of the operation under test. 
        /// </summary>
        private TimeSpan offset;

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// uses outbound samples as a priority over inbound as they are less likely to be malicious.
        /// This means that even when there are many inbound connections, only the first ones are considered until there are enough outbound nodes. 
        /// Eg only 9 inbound per 1 outbound.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSetOfInboundTimeManipulatorsAndLowSampleSetOfOutbound_GetsOverridenByOutboundSamples()
        {
            this.Given_an_empty_time_sync_behaviour_state();
            this.Given_40_inbound_samples_with_offset_of(10);
            this.Given_1_outbound_sample_with_offset_of(20);
            this.When_calculating_time_adjust_offset();
            this.Then_adjusted_time_offset_should_be(20);
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
            this.Then_adjusted_time_offset_should_be(0);
        }

        private void Given_an_empty_time_sync_behaviour_state()
        {
            this.dateTimeProvider = new DateTimeProvider();
            this.timesyncBehaviourState = new TimeSyncBehaviorState(
                this.dateTimeProvider, new NodeLifetime(),
                new AsyncLoopFactory(new LoggerFactory()), 
                new LoggerFactory());
        }

        private void Given_1_outbound_sample_with_offset_of(int offsetSeconds)
        {
            this.timesyncBehaviourState.AddTimeData(IPAddress.Parse("2.2.2.2"), TimeSpan.FromSeconds(offsetSeconds), isInboundConnection: false);
        }

        private void Given_40_inbound_samples_with_offset_of(int offset)
        {
            for (int i = 1; i <= 40; i++)
            {
                this.timesyncBehaviourState.AddTimeData(IPAddress.Parse("1.2.3." + i), TimeSpan.FromSeconds(offset), isInboundConnection: true);
            }
        }

        private void When_calculating_time_adjust_offset()
        {
            var adjustedTime = this.dateTimeProvider.GetAdjustedTime();
            var now = this.dateTimeProvider.GetUtcNow();
            this.offset = adjustedTime - now;
        }

        private void Then_adjusted_time_offset_should_be(int expectedOffset)
        {
            /// It is only rounded so that assertions are easier, a future improvement would be to not use UtcNow directly in the code but to use an indirection, allowing it to be replaced at test time.
            var roundedOffset = (int)Math.Round((decimal) this.offset.TotalMilliseconds, MidpointRounding.AwayFromZero) / 1000;

            Assert.Equal(expectedOffset, roundedOffset);
        }
    }
}