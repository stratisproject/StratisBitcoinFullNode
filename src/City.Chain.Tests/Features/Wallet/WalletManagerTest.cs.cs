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
		private readonly WalletFixture walletFixture;

		public WalletManagerTest(WalletFixture walletFixture)
		{
			this.walletFixture = walletFixture;
		}

		[Fact]
        public void CreateDefaultWalletAndVerify()
        {
            DataFolder dataFolder = CreateDataFolder(this);
            var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwalletname=default", "-defaultwalletpassword=default", "-unlockdefaultwallet");
            walletManager.Start();
            Assert.True(walletManager.ContainsWallets);

            var defaultWallet = walletManager.Wallets.First();

            Assert.Equal("default", defaultWallet.Name);

            // Attempt to load the default wallet.
            var wallet = walletManager.LoadWallet("default", "default");

            Assert.Equal(wallet.EncryptedSeed, defaultWallet.EncryptedSeed);
        }

		[Fact]
		public void VerifyExecutionOfWalletNotify()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			Stratis.Bitcoin.Features.Wallet.Wallet wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			(ExtKey ExtKey, string ExtPubKey) accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet, "password", "m/44'/0'/0'");
			(PubKey PubKey, BitcoinPubKeyAddress Address) spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/0");
			(PubKey PubKey, BitcoinPubKeyAddress Address) destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "0/1");
			(PubKey PubKey, BitcoinPubKeyAddress Address) changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, accountKeys.ExtPubKey, "1/0");

			var spendingAddress = new HdAddress
			{
				Index = 0,
				HdPath = $"m/44'/0'/0'/0/0",
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationAddress = new HdAddress
			{
				Index = 1,
				HdPath = $"m/44'/0'/0'/0/1",
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new HdAddress
			{
				Index = 0,
				HdPath = $"m/44'/0'/0'/1/0",
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			(ChainIndexer chain, uint256 blockhash, Block block) chainInfo = WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);

			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself in a new block.
			Transaction transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress, destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
			Block block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

			wallet.AccountsRoot.ElementAt(0).Accounts.Add(new HdAccount
			{
				Index = 0,
				Name = "account1",
				HdPath = "m/44'/0'/0'",
				ExtendedPubKey = accountKeys.ExtPubKey,
				ExternalAddresses = new List<HdAddress> { spendingAddress, destinationAddress },
				InternalAddresses = new List<HdAddress> { changeAddress }
			});

			var walletFeePolicy = new Mock<IWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var settings = new WalletSettings(NodeSettings.Default(this.Network));
			settings.WalletNotify = "curl -X POST -d txid=%s http://127.0.0.1:8080";

			var walletManager = new WalletManager(this.LoggerFactory.Object, this.Network, chainInfo.chain, settings,
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(), DateTimeProvider.Default, new ScriptAddressReader());
			walletManager.Wallets.Add(wallet);
			walletManager.LoadKeysLookupLock();
			walletManager.WalletTipHash = block.Header.GetHash();

			ChainedHeader chainedBlock = chainInfo.chain.GetHeader(block.GetHash());
			walletManager.ProcessBlock(block, chainedBlock);

			HdAddress spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(transaction.Outputs[1].Value, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

			Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
			TransactionData destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
			Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

			Assert.Equal(1, wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			TransactionData changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

			Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
			Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
			Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
			Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
		}

		//[Fact]
		//public void CreateDefaultWalletAndVerifyCustomPassword()
		//{
		//    DataFolder dataFolder = CreateDataFolder(this);
		//    var walletManager = this.CreateWalletManager(dataFolder, KnownNetworks.StratisMain, "-defaultwallet", "-defaultpassword=mypass");
		//    walletManager.Start();
		//    Assert.True(walletManager.ContainsWallets);

		//    var defaultWallet = walletManager.Wallets.First();

		//    Assert.Equal("default", defaultWallet.Name);

		//    // Attempt to load the default wallet.
		//    var wallet = walletManager.LoadWallet("default", "default");

		//    Assert.Equal(wallet.EncryptedSeed, defaultWallet.EncryptedSeed);
		//}

		private WalletManager CreateWalletManager(DataFolder dataFolder, Network network, params string[] cmdLineArgs)
        {
            var nodeSettings = new NodeSettings(KnownNetworks.RegTest, ProtocolVersion.PROTOCOL_VERSION, network.Name, cmdLineArgs);
            var walletSettings = new WalletSettings(nodeSettings);

            return new WalletManager(this.LoggerFactory.Object, network, new ChainIndexer(network),
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
