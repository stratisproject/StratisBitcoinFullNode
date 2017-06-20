using System;
using NBitcoin;
using Stratis.Bitcoin.Wallet.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratis.Bitcoin.Tests.Wallet
{
    [TestClass]
    public class WalletHelpersTest
    {
        [TestMethod]
        public void GetMainNetworkRetuirnsNetworkMain()
        {
            Network network = WalletHelpers.GetNetwork("main");
            Assert.AreEqual(Network.Main, network);            
        }

        [TestMethod]
        public void GetMainNetNetworkRetuirnsNetworkMain()
        {
            Network network = WalletHelpers.GetNetwork("mainnet");
            Assert.AreEqual(Network.Main, network);
        }

        [TestMethod]
        public void GetTestNetworkRetuirnsNetworkTest()
        {
            Network network = WalletHelpers.GetNetwork("test");
            Assert.AreEqual(Network.TestNet, network);
        }

        [TestMethod]
        public void GetTestNetNetworkRetuirnsNetworkTest()
        {
            Network network = WalletHelpers.GetNetwork("testnet");
            Assert.AreEqual(Network.TestNet, network);
        }

        [TestMethod]
        public void GetNetworkIsCaseInsensitive()
        {
            Network testNetwork = WalletHelpers.GetNetwork("Test");
            Assert.AreEqual(Network.TestNet, testNetwork);

            Network mainNetwork = WalletHelpers.GetNetwork("MainNet");
            Assert.AreEqual(Network.Main, mainNetwork);
        }

        [TestMethod]
        public void WrongNetworkThrowsArgumentException()
        {
            Assert.ThrowsException<ArgumentException>(() => WalletHelpers.GetNetwork("myNetwork"));
        }
    }
}
