using System;
using NBitcoin;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    ///     Providing date time functionality.
    /// </summary>
    public interface IDateTimeProvider
    {
        /// <summary>
        ///     Get the current time in Linux format
        /// </summary>
        long GetTime();

        /// <summary>
        ///     Get the current time offset in UTC.
        /// </summary>
        DateTimeOffset GetTimeOffset();

        /// <summary>
        ///     Get the current time in UTC.
        /// </summary>
        DateTime GetUtcNow();
    }

    public class DateTimeProvider : IDateTimeProvider
    {
        public static IDateTimeProvider Default => new DateTimeProvider();

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
    }
}