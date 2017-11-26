using System;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Commonly used time spans.
    /// </summary>
    public static class TimeSpans
    {
        /// <summary>Time span of 100 milliseconds.</summary>
        public static TimeSpan Ms100 => TimeSpan.FromMilliseconds(100);

        /// <summary>Time span of 1 second.</summary>
        public static TimeSpan Second => TimeSpan.FromSeconds(1);

        /// <summary>Time span of 5 seconds.</summary>
        public static TimeSpan FiveSeconds => TimeSpan.FromSeconds(5);

        /// <summary>Time span of 10 seconds.</summary>
        public static TimeSpan TenSeconds => TimeSpan.FromSeconds(10);

        /// <summary>Time span of 1 minute.</summary>
        public static TimeSpan Minute => TimeSpan.FromMinutes(1);

        /// <summary>
        /// Special time span value used for repeat frequency values, for which it means that
        /// the event should be only run once and not repeated.
        /// </summary>
        public static TimeSpan RunOnce => TimeSpan.FromSeconds(-1);
    }
}
