using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Tests of <see cref="TimeSyncBehavior"/> and <see cref="TimeSyncBehaviorState"/> classes.
    /// </summary>
    public partial class TimeSyncBehaviorAdjustedTimeTest
    {
        /// <summary>
        /// Time data is added this this state. The combination of this and datetimeprovider are what is under test.
        /// </summary>
        private TimeSyncBehaviorState timesyncBehaviourState;

        /// <summary>
        /// This provides the adjusted time ready for assertion. The combination of this and timesyncBehaviorState are what is under test.
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
            given_40_inbound_samples();
            given_1_outbound_sample();
            when_calculating_time_adjust_offset_to_nearest_second();
            then_adjusted_offset_should_be_the_median_of_the_first_9_inbound_and_the_10x_weighted_1_outbound();
        }

        /// <summary>
        /// Checks that <see cref="TimeSyncBehaviorState.AddTimeData(IPAddress, TimeSpan, bool)"/>
        /// uses system time without an offset when there are no outbound samples.
        /// </summary>
        [Fact]
        public void AddTimeData_WithLargeSampleSetOfInboundTimeManipulatorsAndZeroOutbound_SticksWithTheSystemTime()
        {
            given_an_empty_time_sync_behaviour_state();
            given_40_inbound_samples();
            given_0_outbound_samples();
            when_calculating_time_adjust_offset_to_nearest_second();
            then_adjusted_offset_should_be_same_as_the_system_time();
        }
    }
}