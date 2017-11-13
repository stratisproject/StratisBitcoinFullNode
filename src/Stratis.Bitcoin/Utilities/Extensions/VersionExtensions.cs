using System;

namespace Stratis.Bitcoin.Utilities.Extensions
{
    /// <summary>
    /// Extension methods for Version class.
    /// </summary>
    public static class VersionExtensions
    {
        /// <summary>
        /// Converts a version information to integer.
        /// </summary>
        /// <param name="version">Version information to convert.</param>
        /// <returns>Integer representation of the <param name="version"/> information.</returns>
        public static uint ToUint(this Version version)
        {
            return (uint)(version.Major * 1000000u + version.Minor * 10000u + version.Build * 100u + version.Revision);
        }
    }
}
