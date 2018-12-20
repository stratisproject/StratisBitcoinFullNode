using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Protocol;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Tests.Wallet.Common;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Xunit;

namespace City.Chain.Tests.Features.Wallet
{
    public class WalletManagerTest : LogsTestBase, IClassFixture<WalletFixture>
    {
        [Fact]
        public void CreateDefaultWalletAndVerify()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwallet");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            // Attempt to load the default wallet.
            var wallet = walletManager.LoadWallet("default", "default");

            Assert.Equal(wallet.EncryptedSeed, defaultWallet.EncryptedSeed);
        }

        [Fact]
        public void CreateDefaultWalletAndVerifyCustomPassword()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwallet", "-defaultpassword=mypass");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            // Attempt to load the default wallet.
            var wallet = walletManager.LoadWallet("default", "default");

            Assert.Equal(wallet.EncryptedSeed, defaultWallet.EncryptedSeed);
        }

        private WalletManager CreateWalletManager(DataFolder dataFolder, Network network, params string[] cmdLineArgs)
        {
            var nodeSettings = new NodeSettings(KnownNetworks.RegTest, ProtocolVersion.PROTOCOL_VERSION, network.Name, cmdLineArgs);
            var walletSettings = new WalletSettings(nodeSettings);

            return new WalletManager(this.LoggerFactory.Object, network, new ConcurrentChain(network),
                walletSettings, dataFolder, new Mock<IWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
        }
    }

    public class WalletFixture : IDisposable
    {
        private readonly Dictionary<(string, string), Stratis.Bitcoin.Features.Wallet.Wallet> walletsGenerated;

        public WalletFixture()
        {
            this.walletsGenerated = new Dictionary<(string, string), Stratis.Bitcoin.Features.Wallet.Wallet>();
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Creates a new wallet.
        /// </summary>
        /// <remarks>
        /// If it's the first time this wallet is created within this class, it is added to a collection for use by other tests.
        /// If the same parameters have already been used to create a wallet, the wallet will be retrieved from the internal collection and a copy of this wallet will be returned.
        /// </remarks>
        /// <param name="name">The name.</param>
        /// <param name="password">The password.</param>
        /// <returns></returns>
        public Stratis.Bitcoin.Features.Wallet.Wallet GenerateBlankWallet(string name, string password)
        {
            if (this.walletsGenerated.TryGetValue((name, password), out Stratis.Bitcoin.Features.Wallet.Wallet existingWallet))
            {
                string serializedExistingWallet = JsonConvert.SerializeObject(existingWallet, Formatting.None);
                return JsonConvert.DeserializeObject<Stratis.Bitcoin.Features.Wallet.Wallet>(serializedExistingWallet);
            }

            Stratis.Bitcoin.Features.Wallet.Wallet newWallet = WalletTestsHelpers.GenerateBlankWallet(name, password);
            this.walletsGenerated.Add((name, password), newWallet);

            string serializedNewWallet = JsonConvert.SerializeObject(newWallet, Formatting.None);
            return JsonConvert.DeserializeObject<Stratis.Bitcoin.Features.Wallet.Wallet>(serializedNewWallet);
        }
    }
}
