using System;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class DateTimeProviderTest
    {
        /// <summary>
        /// Because we can not make multiple calls to DateTime.UtcNow and guarantee that the result is the same,
        /// we need to check that the returned value falls within a given threshold period. We arbitrarily choose an interval
        /// and assume that the subsequent calls to DateTime.UtcNow will always take less time than that.
        /// </summary>
        public const int TimeIntervalThresholdSeconds = 2;

        [Fact]
        public void GetUtcNowReturnsCurrentUtcDateTime()
        {
            DateTime result = DateTimeProvider.Default.GetUtcNow();

            var now = DateTime.UtcNow;

            Assert.True(now >= result && now < result.AddSeconds(TimeIntervalThresholdSeconds));
        }

        [Fact]
        public void GetTimeOffsetReturnsCurrentUtcTimeOffset()
        {
            DateTimeOffset result = DateTimeProvider.Default.GetTimeOffset();

            var now = DateTimeOffset.UtcNow;

            Assert.True(now >= result && now < result.AddSeconds(TimeIntervalThresholdSeconds));
        }

        [Fact]
        public void GetTimeReturnsUnixTimeStamp()
        {
            long timeStamp = DateTimeProvider.Default.GetTime();

            var now = DateTime.UtcNow.ToUnixTimestamp();
            Assert.True(now >= timeStamp && now < timeStamp + TimeIntervalThresholdSeconds);
        }
    }
}
