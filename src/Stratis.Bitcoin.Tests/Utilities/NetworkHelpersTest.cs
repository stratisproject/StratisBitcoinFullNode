using System;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class NetworkHelpersTest
    {
        [Fact]
        public void GetMainNetworkReturnsNetworkMain()
        {
            Network main = NBitcoin.Networks.Main;
            Network network = NetworkHelpers.GetNetwork("main");
            Assert.Equal(NBitcoin.Networks.Main, network);
        }

        [Fact]
        public void GetMainNetNetworkReturnsNetworkMain()
        {
            Network main = NBitcoin.Networks.Main;
            Network network = NetworkHelpers.GetNetwork("mainnet");
            Assert.Equal(NBitcoin.Networks.Main, network);
        }

        [Fact]
        public void GetTestNetworkReturnsNetworkTest()
        {
            Network test = NBitcoin.Networks.TestNet;
            Network network = NetworkHelpers.GetNetwork("test");
            Assert.Equal(NBitcoin.Networks.TestNet, network);
        }

        [Fact]
        public void GetTestNetNetworkReturnsNetworkTest()
        {
            Network test = NBitcoin.Networks.TestNet;
            Network network = NetworkHelpers.GetNetwork("testnet");
            Assert.Equal(NBitcoin.Networks.TestNet, network);
        }

        [Fact]
        public void GetNetworkIsCaseInsensitive()
        {
            Network test = NBitcoin.Networks.TestNet;
            Network main = NBitcoin.Networks.Main;

            Network testNetwork = NetworkHelpers.GetNetwork("Test");
            Assert.Equal(NBitcoin.Networks.TestNet, testNetwork);

            Network mainNetwork = NetworkHelpers.GetNetwork("MainNet");
            Assert.Equal(NBitcoin.Networks.Main, mainNetwork);
        }

        [Fact]
        public void WrongNetworkThrowsArgumentException()
        {
            Exception exception = Record.Exception(() => NetworkHelpers.GetNetwork("myNetwork"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }
    }
}
