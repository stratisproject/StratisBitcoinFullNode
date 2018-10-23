using System;
using Stratis.Bitcoin.Utilities.Extensions;
using TracerAttributes;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Providing date time functionality.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        /// Get the current time in Linux format.
        /// </summary>
        long GetTime();

        /// <summary>
        /// Get the current time offset in UTC.
        /// </summary>
        DateTimeOffset GetTimeOffset();

        /// <summary>
        /// Get the current time in UTC.
        /// </summary>
        DateTime GetUtcNow();

        /// <summary>
        /// Obtains adjusted time, which is time synced with network peers.
        /// </summary>
        /// <returns>Adjusted UTC timestamp.</returns>
        DateTime GetAdjustedTime();

        /// <summary>
        /// Obtains adjusted time, which is time synced with network peers, as Unix timestamp with seconds precision.
        /// </summary>
        /// <returns>Adjusted UTC timestamp as Unix timestamp with seconds precision.</returns>
        long GetAdjustedTimeAsUnixTimestamp();

        /// <summary>
        /// Sets adjusted time offset, which is time difference from network peers.
        /// </summary>
        /// <param name="adjustedTimeOffset">Offset to adjust time with.</param>
        void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset);
    }

    /// <inheritdoc />
    [NoTrace]
    public class DateTimeProvider : IDateTimeProvider
    {
        /// <summary>Static instance of the object to prevent the need of creating new instance.</summary>
        public static IDateTimeProvider Default { get; }

        /// <summary>UTC adjusted timestamp, or null if no adjusted time is set.</summary>
        protected TimeSpan adjustedTimeOffset;

        /// <summary>
        /// Initializes a default instance of the object.
        /// </summary>
        static DateTimeProvider()
        {
            Default = new DateTimeProvider();
        }

        /// <summary>
        /// Initializes instance of the object.
        /// </summary>
        public DateTimeProvider()
        {
            this.adjustedTimeOffset = TimeSpan.Zero;
        }

        /// <inheritdoc />
        public virtual long GetTime()
        {
            return DateTime.UtcNow.ToUnixTimestamp();
        }

        /// <inheritdoc />
        public virtual DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }

        /// <inheritdoc />
        public virtual DateTimeOffset GetTimeOffset()
        {
            return DateTimeOffset.UtcNow;
        }

        /// <inheritdoc />
        public DateTime GetAdjustedTime()
        {
            return this.GetUtcNow().Add(this.adjustedTimeOffset);
        }

        /// <inheritdoc />
        public long GetAdjustedTimeAsUnixTimestamp()
        {
            return new DateTimeOffset(this.GetAdjustedTime()).ToUnixTimeSeconds();
        }

        /// <inheritdoc />
        public void SetAdjustedTimeOffset(TimeSpan adjustedTimeOffset)
        {
            this.adjustedTimeOffset = adjustedTimeOffset;
        }
    }
}
