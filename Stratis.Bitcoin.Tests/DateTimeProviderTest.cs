using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests
{
    [TestClass]
    public class DateTimeProviderTest
    {
        [TestMethod]
        public void GetUtcNowReturnsCurrentUtcDateTime()
        {
            var result = DateTimeProvider.Default.GetUtcNow();

            Assert.AreEqual(DateTime.UtcNow.ToString("yyyyMMddhhmmss"), result.ToString("yyyyMMddhhmmss"));
        }

        [TestMethod]
        public void GetTimeOffsetReturnsCurrentUtcTimeOffset()
        {
            var result = DateTimeProvider.Default.GetTimeOffset();

            Assert.AreEqual(DateTimeOffset.UtcNow, result);
        }

        [TestMethod]
        public void GetTimeReturnsUnixTimeStamp()
        {
            var timeStamp = DateTimeProvider.Default.GetTime();

            Assert.AreEqual(DateTime.UtcNow.ToUnixTimestamp(), timeStamp);
        }
    }
}
