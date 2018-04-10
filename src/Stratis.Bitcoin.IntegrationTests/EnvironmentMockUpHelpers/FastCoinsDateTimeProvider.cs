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

        public long GetTime()
        {
            return this.startFrom.ToUnixTimestamp();
        }

        public DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        /// <summary>
        /// This gets called when the Transaction's time gets set.
        /// </summary>
        public DateTime GetAdjustedTime()
        {
            this.startFrom = this.startFrom.AddSeconds(5);
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the Block Header's time gets set.
        /// <para>
        /// Add 5 seconds to the time so that the block header's time stamp is after
        /// the transaction's creation time.
        /// </para>
        /// </summary>
        public DateTimeOffset GetTimeOffset()
        {
            this.startFrom = this.startFrom.AddSeconds(1);
            return this.startFrom;
        }

        /// <summary>
        /// This gets called when the coin stake block gets created.
        /// </summary>
        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return new DateTimeOffset(this.startFrom.AddMinutes(118)).ToUnixTimeSeconds();
        }

        public void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset)
        {
            this.adjustedTimeOffset = adjustedTimeOffset;
        }
    }
}