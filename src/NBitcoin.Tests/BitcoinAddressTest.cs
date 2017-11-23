using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NBitcoin.Tests
{
	public class BitcoinAddressTest
	{
        public BitcoinAddressTest()
        {
            // These flags may get set due to static network initializers
            // which include the initializers for Stratis.
            Transaction.TimeStamp = false;
            Block.BlockSignature = false;
        }

		[Fact]
		[Trait("UnitTest", "UnitTest")]
		public void ShouldThrowBase58Exception()
		{
			String key = "";
			Assert.Throws<FormatException>(() => BitcoinAddress.Create(key, Network.Main));

			key = null;
			Assert.Throws<ArgumentNullException>(() => BitcoinAddress.Create(key, Network.Main));
		}
	}
}
