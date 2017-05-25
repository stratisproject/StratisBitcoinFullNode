using System;

namespace Stratis.Bitcoin.Utilities
{
	public static class TimeSpans
    {
        public static TimeSpan Mls100 => TimeSpan.FromMilliseconds(100);
        public static TimeSpan Second => TimeSpan.FromSeconds(1);
        public static TimeSpan FiveSeconds => TimeSpan.FromSeconds(5);
        public static TimeSpan TenSeconds => TimeSpan.FromSeconds(10);
		public static TimeSpan RunOnce => TimeSpan.FromSeconds(-1);
	}

    public static class VersionExtensions
    {
        public static uint ToUint(this Version version)
        {
            return (uint)(version.Major * 1000000u + version.Minor * 10000u + version.Build * 100u + version.Revision);
        }
    }
}
