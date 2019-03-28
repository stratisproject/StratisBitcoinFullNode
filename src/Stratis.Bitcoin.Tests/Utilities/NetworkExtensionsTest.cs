using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class NetworkExtensionsTest
    {
        [Fact]
        public void BitcoinNetworksAreBitcoin()
        {
            Assert.True(Networks.Networks.Bitcoin.Mainnet().IsBitcoin());
            Assert.True(Networks.Networks.Bitcoin.Mainnet().IsBitcoin());
            Assert.True(Networks.Networks.Bitcoin.Mainnet().IsBitcoin());
        }

        [Fact]
        public void OtherNetworksArentBitcoin()
        {
            Assert.False(Networks.Networks.Stratis.Mainnet().IsBitcoin());
            Assert.False(Networks.Networks.Stratis.Testnet().IsBitcoin());
            Assert.False(Networks.Networks.Stratis.Regtest().IsBitcoin());

            Assert.False(new ImaginaryPoANetwork().IsBitcoin());
            Assert.False(new ImaginaryCirrusNetwork().IsBitcoin());
        }

        private class ImaginaryPoANetwork : Network
        {
            public ImaginaryPoANetwork()
            {
                this.Name = "PoANetwork";
            }
        }

        private class ImaginaryCirrusNetwork : Network
        {
            public ImaginaryCirrusNetwork()
            {
                this.Name = "Cirrus";
            }
        }
    }
}
