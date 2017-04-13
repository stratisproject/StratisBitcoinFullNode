using System;
using System.Collections.Generic;
using System.Text;
using Breeze.Wallet.Helpers;
using NBitcoin;
using Xunit;

namespace Breeze.Api.Tests
{
    public class WalletHelpersTest
    {
        [Fact]
        public void GetMainNetworkRetuirnsNetworkMain()
        {
            Network network = WalletHelpers.GetNetwork("main");
            Assert.Equal(Network.Main, network);            
        }

        [Fact]
        public void GetMainNetNetworkRetuirnsNetworkMain()
        {
            Network network = WalletHelpers.GetNetwork("mainnet");
            Assert.Equal(Network.Main, network);
        }

        [Fact]
        public void GetTestNetworkRetuirnsNetworkTest()
        {
            Network network = WalletHelpers.GetNetwork("test");
            Assert.Equal(Network.TestNet, network);
        }

        [Fact]
        public void GetTestNetNetworkRetuirnsNetworkTest()
        {
            Network network = WalletHelpers.GetNetwork("testnet");
            Assert.Equal(Network.TestNet, network);
        }

        [Fact]
        public void GetNetworkIsCaseInsensitive()
        {
            Network testNetwork = WalletHelpers.GetNetwork("Test");
            Assert.Equal(Network.TestNet, testNetwork);

            Network mainNetwork = WalletHelpers.GetNetwork("MainNet");
            Assert.Equal(Network.Main, mainNetwork);
        }

        [Fact]
        public void WrongNetworkThrowsArgumentException()
        {
            var exception = Record.Exception(() => WalletHelpers.GetNetwork("myNetwork"));
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }
    }
}
