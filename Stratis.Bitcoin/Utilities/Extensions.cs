using System;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Commonly used time spans.
    /// </summary>
    public static class TimeSpans
    {
        /// <summary>Time span of 100 milliseconds.</summary>
        /// <remarks>TODO: Usually milliseconds are shorted to "ms", not "mls", so I suggest renaming this to "Ms100".</remarks>
        public static TimeSpan Mls100 => TimeSpan.FromMilliseconds(100);

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

    /// <summary>
    /// Extension methods for Version class.
    /// </summary>
    public static class VersionExtensions
    {
        /// <summary>
        /// Converts a version information to integer.
        /// </summary>
        /// <param name="version">Version information to convert.</param>
        /// <returns>Integer representation of the <param name="version"> information.</returns>
        public static uint ToUint(this Version version)
        {
            return (uint)(version.Major * 1000000u + version.Minor * 10000u + version.Build * 100u + version.Revision);
        }
    }

    /// <summary>
    /// Extension methods for arguments array.
    /// </summary>
    public static class ArgsExtensions
    {
        /// <summary>
        /// Obtains a value of command line argument.
        /// <para>
        /// It is expected that arguments are written on command line as <c>argName=argValue</c>, 
        /// where argName usually (but does not need to) starts with "-".
        /// </para>
        /// <para>
        /// The argValue can be wrapped with '"' quotes from both sides, in which case the quotes are removed, 
        /// but it is not allowed for argValue to contain '"' inside the actual value.
        /// </para>
        /// </summary>
        /// <param name="args">Application command line arguments.</param>
        /// <param name="arg">Name of the command line argument which value should be obtained.</param>
        /// <returns>Value of the specified argument or null if no such argument is found among the given list of arguments.</returns>
        public static string GetValueOf(this string[] args, string arg)
        {
            return args.Where(a => a.StartsWith($"{arg}=")).Select(a => a.Substring($"{arg}=".Length).Replace("\"", "")).FirstOrDefault();
        }
    }
}
