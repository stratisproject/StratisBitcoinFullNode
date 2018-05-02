using System;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    /// <summary>
    /// Provides a set of extension methods for the <see cref="DateTime"/> class.
    /// </summary>
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a given DateTime into a Unix timestamp.
        /// </summary>
        /// <param name="value">Any DateTime</param>
        /// <returns>The given DateTime in Unix timestamp format</returns>
        /// <remarks>This represents the number of seconds that have elapsed since 1970-01-01T00:00:00Z.</remarks>
        public static int ToUnixTimestamp(this DateTime value)
        {
            return (int)((DateTimeOffset)value).ToUnixTimeSeconds();
        }
    }
}