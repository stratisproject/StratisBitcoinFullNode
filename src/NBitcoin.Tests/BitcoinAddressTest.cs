using System;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class BitcoinAddressTest
    {
        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void ShouldThrowBase58Exception()
        {
            string key = "";
            Assert.Throws<FormatException>(() => BitcoinAddress.Create(key, KnownNetworks.Main));

            key = null;
            Assert.Throws<ArgumentNullException>(() => BitcoinAddress.Create(key, KnownNetworks.Main));
        }
    }
}
