using System;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace NBitcoin
{
    public class DateTimeProviderTest
    {
        [Fact]
        public void GetUtcNowReturnsCurrentUtcDateTime()
        {
            int timeOffset = -96;
            DateTimeProvider.Default.SetSystemTimeOffset(timeOffset);
            DateTime result = DateTimeProvider.Default.GetUtcNow();

            Assert.Equal(DateTime.UtcNow.AddSeconds(timeOffset).ToString("yyyyMMddhhmmss"), result.ToString("yyyyMMddhhmmss"));
        }

        [Fact]
        public void GetTimeOffsetReturnsCurrentUtcTimeOffset()
        {
            DateTimeOffset result = DateTimeProvider.Default.GetTimeOffset();

            Assert.Equal(DateTimeOffset.UtcNow.ToString("yyyyMMddhhmmss"), result.ToString("yyyyMMddhhmmss"));
        }

        [Fact]
        public void GetTimeReturnsUnixTimeStamp()
        {
            long timeStamp = DateTimeProvider.Default.GetTime();

            Assert.Equal(DateTimeProvider.Default.GetUtcNow().ToUnixTimestamp(), timeStamp);
        }
    }
}
