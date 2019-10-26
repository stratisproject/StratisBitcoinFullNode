using System;
using NBitcoin;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    public class NetworkHelpersTest
    {
        [Fact]
        public void GetMainNetworkReturnsNetworkMain()
        {
            Network main = KnownNetworks.Main;
            Network network = NetworkHelpers.GetNetwork("main");
            Assert.Equal(KnownNetworks.Main, network);
        }

        [Fact]
        public void GetMainNetNetworkReturnsNetworkMain()
        {
            Network main = KnownNetworks.Main;
            Network network = NetworkHelpers.GetNetwork("mainnet");
            Assert.Equal(KnownNetworks.Main, network);
        }

        [Fact]
        public void GetTestNetworkReturnsNetworkTest()
        {
            Network test = KnownNetworks.TestNet;
            Network network = NetworkHelpers.GetNetwork("test");
            Assert.Equal(KnownNetworks.TestNet, network);
        }

        [Fact]
        public void GetTestNetNetworkReturnsNetworkTest()
        {
            Network test = KnownNetworks.TestNet;
            Network network = NetworkHelpers.GetNetwork("testnet");
            Assert.Equal(KnownNetworks.TestNet, network);
        }

        [Fact]
        public void GetNetworkIsCaseInsensitive()
        {
            Network test = KnownNetworks.TestNet;
            Network main = KnownNetworks.Main;

            Network testNetwork = NetworkHelpers.GetNetwork("Test");
            Assert.Equal(KnownNetworks.TestNet, testNetwork);

            Network mainNetwork = NetworkHelpers.GetNetwork("MainNet");
            Assert.Equal(KnownNetworks.Main, mainNetwork);
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
