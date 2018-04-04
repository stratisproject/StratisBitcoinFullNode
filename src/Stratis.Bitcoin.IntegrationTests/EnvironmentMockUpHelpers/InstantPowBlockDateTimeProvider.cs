using System;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public sealed class InstantPowBlockDateTimeProvider : IDateTimeProvider
    {
        private TimeSpan adjustedTimeOffset;
        private DateTime startFrom;

        public InstantPowBlockDateTimeProvider()
        {
            this.adjustedTimeOffset = TimeSpan.Zero;
            this.startFrom = new DateTime(2018, 1, 1);
        }

        /// <summary>
        /// This gets called when the Transaction's Time gets set.
        /// </summary>
        public long GetTime()
        {
            this.startFrom = this.startFrom.AddSeconds(70);
            return this.startFrom.ToUnixTimestamp();
        }

        public DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// This gets called when we set the Block Header's time.
        /// </summary>
        public DateTimeOffset GetTimeOffset()
        {
            return this.startFrom.AddSeconds(5);
        }

        public DateTime GetAdjustedTime()
        {
            return this.GetUtcNow().Add(this.adjustedTimeOffset);
        }

        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset)
        {
            this.adjustedTimeOffset = adjustedTimeOffset;
        }
    }
}