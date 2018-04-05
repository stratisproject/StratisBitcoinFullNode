using System;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public sealed class FastCoinsDateTimeProvider : IDateTimeProvider
    {
        private TimeSpan adjustedTimeOffset;
        private DateTime startFrom;

        public FastCoinsDateTimeProvider()
        {
            this.adjustedTimeOffset = TimeSpan.Zero;
            this.startFrom = new DateTime(2018, 1, 1);
        }

        /// <summary>
        /// This gets called when the Transaction's time gets set.
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
        /// This gets called when the Block Header's time gets set.
        /// </summary>
        public DateTimeOffset GetTimeOffset()
        {
            return this.startFrom.AddSeconds(5);
        }

        public DateTime GetAdjustedTime()
        {
            return this.startFrom.Add(this.adjustedTimeOffset);
        }

        /// <summary>
        /// This gets called when the coin stake block gets created.
        /// </summary>
        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return new DateTimeOffset(this.GetAdjustedTime().AddMinutes(119)).ToUnixTimeSeconds();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset)
        {
            this.adjustedTimeOffset = adjustedTimeOffset;
        }
    }
}