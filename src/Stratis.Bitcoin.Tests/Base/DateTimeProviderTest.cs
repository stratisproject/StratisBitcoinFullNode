using System;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class DateTimeProviderTest
    {
        [Fact]
        public void GetUtcNowReturnsCurrentUtcDateTime()
        {
            var result = DateTimeProvider.Default.GetUtcNow();

            Assert.Equal(DateTime.UtcNow.ToString("yyyyMMddhhmmss"), result.ToString("yyyyMMddhhmmss"));
        }

        [Fact]
        public void GetTimeOffsetReturnsCurrentUtcTimeOffset()
        {
            var result = DateTimeProvider.Default.GetTimeOffset();

            Assert.Equal(DateTimeOffset.UtcNow.ToString("yyyyMMddhhmmss"), result.ToString("yyyyMMddhhmmss"));
        }

        [Fact]
        public void GetTimeReturnsUnixTimeStamp()
        {
            var timeStamp = DateTimeProvider.Default.GetTime();

            Assert.Equal(DateTime.UtcNow.ToUnixTimestamp(), timeStamp);
        }
    }
}
