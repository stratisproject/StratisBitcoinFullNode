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
            Network main = Networks.Main;
            Network network = NetworkHelpers.GetNetwork("main");
            Assert.Equal(Networks.Main, network);
        }

        [Fact]
        public void GetMainNetNetworkReturnsNetworkMain()
        {
            Network main = Networks.Main;
            Network network = NetworkHelpers.GetNetwork("mainnet");
            Assert.Equal(Networks.Main, network);
        }

        [Fact]
        public void GetTestNetworkReturnsNetworkTest()
        {
            Network test = Networks.TestNet;
            Network network = NetworkHelpers.GetNetwork("test");
            Assert.Equal(Networks.TestNet, network);
        }

        [Fact]
        public void GetTestNetNetworkReturnsNetworkTest()
        {
            Network test = Networks.TestNet;
            Network network = NetworkHelpers.GetNetwork("testnet");
            Assert.Equal(Networks.TestNet, network);
        }

        [Fact]
        public void GetNetworkIsCaseInsensitive()
        {
            Network test = Networks.TestNet;
            Network main = Networks.Main;

            Network testNetwork = NetworkHelpers.GetNetwork("Test");
            Assert.Equal(Networks.TestNet, testNetwork);

            Network mainNetwork = NetworkHelpers.GetNetwork("MainNet");
            Assert.Equal(Networks.Main, mainNetwork);
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
