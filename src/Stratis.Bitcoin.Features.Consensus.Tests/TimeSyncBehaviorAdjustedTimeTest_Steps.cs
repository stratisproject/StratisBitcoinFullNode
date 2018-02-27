using System;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Utilities;
using Xunit;
using System.Net;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public partial class TimeSyncBehaviorAdjustedTimeTest
    {
        private void given_an_empty_time_sync_behaviour_state()
        {
            this.dateTimeProvider = new DateTimeProvider();
            var state = new TimeSyncBehaviorState(this.dateTimeProvider, new NodeLifetime(),
                new AsyncLoopFactory(new LoggerFactory()), new LoggerFactory());
            this.timesyncBehaviourState = state;
        }

        private void given_1_outbound_sample()
        {
            this.timesyncBehaviourState.AddTimeData(IPAddress.Parse("2.2.2.2"), TimeSpan.FromSeconds(20), isInboundConnection: false);
        }

        private void given_0_outbound_samples()
        {
        }

        private void given_40_inbound_samples()
        {
            for (int i = 1; i <= 40; i++)
            {
                this.timesyncBehaviourState.AddTimeData(IPAddress.Parse("1.2.3." + i), TimeSpan.FromSeconds(10), isInboundConnection: true);
            }
        }

        private void when_calculating_time_adjust_offset_to_nearest_second()
        {
            var adjustedTime = this.dateTimeProvider.GetAdjustedTime();
            var now = this.dateTimeProvider.GetUtcNow();
            var offset = adjustedTime - now;

            this.roundedOffset = (int)Math.Round((decimal) offset.TotalMilliseconds, MidpointRounding.AwayFromZero) / 1000;
        }

        private void then_adjusted_offset_should_be_the_median_of_the_first_9_inbound_and_the_10x_weighted_1_outbound()
        {
            Assert.Equal(20, this.roundedOffset);
        }

        private void then_adjusted_offset_should_be_same_as_the_system_time()
        {
            Assert.Equal(0, this.roundedOffset);
        }
    }
}