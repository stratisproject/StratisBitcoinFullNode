using System;
using NBitcoin;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class NetworkHelpersTest
    {
        [Fact]
        public void GetMainNetworkReturnsNetworkMain()
        {
            Network main = NetworkContainer.Main;
            Network network = NetworkHelpers.GetNetwork("main");
            Assert.Equal(NetworkContainer.Main, network);
        }

        [Fact]
        public void GetMainNetNetworkReturnsNetworkMain()
        {
            Network main = NetworkContainer.Main;
            Network network = NetworkHelpers.GetNetwork("mainnet");
            Assert.Equal(NetworkContainer.Main, network);
        }

        [Fact]
        public void GetTestNetworkReturnsNetworkTest()
        {
            Network test = NetworkContainer.TestNet;
            Network network = NetworkHelpers.GetNetwork("test");
            Assert.Equal(NetworkContainer.TestNet, network);
        }

        [Fact]
        public void GetTestNetNetworkReturnsNetworkTest()
        {
            Network test = NetworkContainer.TestNet;
            Network network = NetworkHelpers.GetNetwork("testnet");
            Assert.Equal(NetworkContainer.TestNet, network);
        }

        [Fact]
        public void GetNetworkIsCaseInsensitive()
        {
            Network test = NetworkContainer.TestNet;
            Network main = NetworkContainer.Main;

            Network testNetwork = NetworkHelpers.GetNetwork("Test");
            Assert.Equal(NetworkContainer.TestNet, testNetwork);

            Network mainNetwork = NetworkHelpers.GetNetwork("MainNet");
            Assert.Equal(NetworkContainer.Main, mainNetwork);
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
