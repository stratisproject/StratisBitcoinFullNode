using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Tests.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonConverters;
using Xunit;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Tests
{
	public class GeneralPurposeWalletManagerTest : LogsTestBase, IDisposable, IClassFixture<GeneralPurposeWalletFixture>
	{
		private readonly GeneralPurposeWalletFixture walletFixture;

		public GeneralPurposeWalletManagerTest(GeneralPurposeWalletFixture walletFixture)
		{
			this.walletFixture = walletFixture;

			// These flags are being set on an individual test case basis.
			// Assume the default values for the static flags.
			Transaction.TimeStamp = false;
			Block.BlockSignature = false;
		}

		public void Dispose()
		{
			// This is needed here because of the fact that the Stratis network, when initialized, sets the
			// Transaction.TimeStamp value to 'true' (look in Network.InitStratisTest() and Network.InitStratisMain()) in order
			// for proof-of-stake to work.
			// Now, there are a few tests where we're trying to parse Bitcoin transaction, but since the TimeStamp is set the true,
			// the execution path is different and the bitcoin transaction tests are failing.
			// Here we're resetting the TimeStamp after every test so it doesn't cause any trouble.
			Transaction.TimeStamp = false;
			Block.BlockSignature = false;
		}

		[Fact]
		public void ProcessTransactionWithValidTransactionLoadsTransactionsIntoWalletIfMatching()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
			var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

			var spendingAddress = new GeneralPurposeAddress
			{
				PrivateKey = spendingKeys.PrivateKey,
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationAddress = new GeneralPurposeAddress
			{
				PrivateKey = destinationKeys.PrivateKey,
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new GeneralPurposeAddress
			{
				PrivateKey = changeKeys.PrivateKey,
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			var chainInfo =
				WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself
			var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress,
				destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain,
				NodeSettings.Default(),
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(),
				DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			walletManager.ProcessTransaction(transaction);

			var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(transaction.Outputs[1].Value,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
			var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1)
				.Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
			Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0)
				.Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
		}

		[Fact]
		public void ProcessTransactionWithEmptyScriptInTransactionDoesNotAddTransactionToWallet()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
			var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

			var spendingAddress = new GeneralPurposeAddress
			{
				PrivateKey = spendingKeys.PrivateKey,
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationAddress = new GeneralPurposeAddress
			{
				PrivateKey = destinationKeys.PrivateKey,
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new GeneralPurposeAddress
			{
				PrivateKey = changeKeys.PrivateKey,
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			var chainInfo =
				WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself
			var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress,
				destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
			transaction.Outputs.ElementAt(1).Value = Money.Zero;
			transaction.Outputs.ElementAt(1).ScriptPubKey = Script.Empty;

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain,
				NodeSettings.Default(),
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(),
				DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			walletManager.ProcessTransaction(transaction);

			var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);

			Assert.Equal(0,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0)
				.Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
		}

		[Fact]
		public void ProcessTransactionWithDestinationToChangeAddressDoesNotAddTransactionAsPayment()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
			var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

			var spendingAddress = new GeneralPurposeAddress
			{
				PrivateKey = spendingKeys.PrivateKey,
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new GeneralPurposeAddress
			{
				PrivateKey = changeKeys.PrivateKey,
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationChangeAddress = new GeneralPurposeAddress
			{
				PrivateKey = destinationKeys.PrivateKey,
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			var chainInfo =
				WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself
			var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress,
				destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain,
				NodeSettings.Default(),
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(),
				DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			walletManager.ProcessTransaction(transaction);

			var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(0, spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.Count);
			Assert.Equal(1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0)
				.Transactions.ElementAt(0);
			Assert.Null(destinationAddressResult.BlockHeight);
			Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, destinationAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, destinationAddressResult.ScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.Count);
			var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1)
				.Transactions.ElementAt(0);
			Assert.Null(destinationAddressResult.BlockHeight);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[1].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, changeAddressResult.ScriptPubKey);
		}

		[Fact]
		public void ProcessTransactionWithBlockHeightSetsBlockHeightOnTransactionData()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
			var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

			var spendingAddress = new GeneralPurposeAddress
			{
				PrivateKey = spendingKeys.PrivateKey,
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationAddress = new GeneralPurposeAddress
			{
				PrivateKey = destinationKeys.PrivateKey,
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new GeneralPurposeAddress
			{
				PrivateKey = changeKeys.PrivateKey,
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			var chainInfo =
				WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself
			var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress,
				destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain,
				NodeSettings.Default(),
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(),
				DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

			var blockHeight = chainInfo.chain.GetBlock(block.GetHash()).Height;
			walletManager.ProcessTransaction(transaction, blockHeight);

			var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(transaction.Outputs[1].Value,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
			Assert.Equal(blockHeight - 1, spentAddressResult.Transactions.ElementAt(0).BlockHeight);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
			var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1)
				.Transactions.ElementAt(0);
			Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
			Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
			Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0)
				.Transactions.ElementAt(0);
			Assert.Equal(blockHeight, destinationAddressResult.BlockHeight);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
		}

		[Fact]
		public void ProcessTransactionWithBlockSetsBlockHash()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
			var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

			var spendingAddress = new GeneralPurposeAddress
			{
				PrivateKey = spendingKeys.PrivateKey,
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationAddress = new GeneralPurposeAddress
			{
				PrivateKey = destinationKeys.PrivateKey,
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new GeneralPurposeAddress
			{
				PrivateKey = changeKeys.PrivateKey,
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			var chainInfo =
				WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);
			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself
			var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress,
				destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain,
				NodeSettings.Default(),
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(),
				DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

			walletManager.ProcessTransaction(transaction, block: block);

			var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(transaction.Outputs[1].Value,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);
			Assert.Equal(chainInfo.block.GetHash(), spentAddressResult.Transactions.ElementAt(0).BlockHash);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
			var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1)
				.Transactions.ElementAt(0);
			Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
			Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
			Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0)
				.Transactions.ElementAt(0);
			Assert.Equal(block.GetHash(), destinationAddressResult.BlockHash);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);
		}

		[Fact]
		public void RemoveBlocksRemovesTransactionsWithHigherBlockHeightAndUpdatesLastSyncedBlockHeight()
		{
			var concurrentchain = new ConcurrentChain(Network.Main);
			var chainedBlock = WalletTestsHelpers.AppendBlock(null, concurrentchain).ChainedBlock;
			chainedBlock = WalletTestsHelpers.AppendBlock(chainedBlock, concurrentchain).ChainedBlock;
			chainedBlock = WalletTestsHelpers.AppendBlock(chainedBlock, concurrentchain).ChainedBlock;

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AccountsRoot.ElementAt(0).Accounts.Add(new GeneralPurposeAccount
			{
				Name = "First account",
				ExternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 2, 3, 4, 5).ToList(),
				InternalAddresses = WalletTestsHelpers.CreateSpentTransactionsOfBlockHeights(Network.Main, 1, 2, 3, 4, 5).ToList()
			});

			// reorg at block 3

			// Trx at block 0 is not spent
			wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0).Transactions.First()
				.SpendingDetails = null;
			wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.First()
				.SpendingDetails = null;

			// Trx at block 2 is spent in block 3, after reorg it will not be spendable.
			wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.First()
				.SpendingDetails.BlockHeight = 3;
			wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(1).Transactions.First()
				.SpendingDetails.BlockHeight = 3;

			// Trx at block 3 is spent at block 5, after reorg it will be spendable.
			wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(2).Transactions.First()
				.SpendingDetails.BlockHeight = 5;
			wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(2).Transactions.First()
				.SpendingDetails.BlockHeight = 5;

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main,
				new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
				CreateDataFolder(this), new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object,
				new NodeLifetime(), DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			walletManager.RemoveBlocks(chainedBlock);

			Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
			Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
			Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
			Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);

			var account = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0);

			Assert.Equal(6, account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).Count());
			Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions)
				.All(r => r.BlockHeight <= chainedBlock.Height));
			Assert.True(account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions).All(r =>
				r.SpendingDetails == null || r.SpendingDetails.BlockHeight <= chainedBlock.Height));
			Assert.Equal(4,
				account.InternalAddresses.Concat(account.ExternalAddresses).SelectMany(r => r.Transactions)
					.Count(t => t.SpendingDetails == null));
		}

		[Fact]
		public void ProcessBlockWithoutWalletsSetsWalletTipToBlockHash()
		{
			var concurrentchain = new ConcurrentChain(Network.Main);
			var blockResult = WalletTestsHelpers.AppendBlock(null, concurrentchain);

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main,
				new Mock<ConcurrentChain>().Object, NodeSettings.Default(),
				CreateDataFolder(this), new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object,
				new NodeLifetime(), DateTimeProvider.Default);

			walletManager.ProcessBlock(blockResult.Block, blockResult.ChainedBlock);

			Assert.Equal(blockResult.ChainedBlock.HashBlock, walletManager.WalletTipHash);
		}

		[Fact]
		public void ProcessBlockWithWalletsProcessesTransactionsOfBlockToWallet()
		{
			DataFolder dataFolder = CreateDataFolder(this);
			Directory.CreateDirectory(dataFolder.WalletPath);

			var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");
			wallet.AddNewAccount("account1", (CoinType)wallet.Network.Consensus.CoinType, DateTimeOffset.UtcNow);
			var accountKeys = WalletTestsHelpers.GenerateAccountKeys(wallet);
			var spendingKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var destinationKeys = WalletTestsHelpers.GenerateAddressKeys(wallet);
			var changeKeys = WalletTestsHelpers.GenerateAddressKeys(wallet, true);

			var spendingAddress = new GeneralPurposeAddress
			{
				PrivateKey = spendingKeys.PrivateKey,
				Address = spendingKeys.Address.ToString(),
				Pubkey = spendingKeys.PubKey.ScriptPubKey,
				ScriptPubKey = spendingKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var destinationAddress = new GeneralPurposeAddress
			{
				PrivateKey = destinationKeys.PrivateKey,
				Address = destinationKeys.Address.ToString(),
				Pubkey = destinationKeys.PubKey.ScriptPubKey,
				ScriptPubKey = destinationKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			var changeAddress = new GeneralPurposeAddress
			{
				PrivateKey = changeKeys.PrivateKey,
				Address = changeKeys.Address.ToString(),
				Pubkey = changeKeys.PubKey.ScriptPubKey,
				ScriptPubKey = changeKeys.Address.ScriptPubKey,
				Transactions = new List<TransactionData>()
			};

			//Generate a spendable transaction
			var chainInfo =
				WalletTestsHelpers.CreateChainAndCreateFirstBlockWithPaymentToAddress(wallet.Network, spendingAddress);

			TransactionData spendingTransaction = WalletTestsHelpers.CreateTransactionDataFromFirstBlock(chainInfo);
			spendingAddress.Transactions.Add(spendingTransaction);

			// setup a payment to yourself in a new block.
			var transaction = WalletTestsHelpers.SetupValidTransaction(wallet, "password", spendingAddress,
				destinationKeys.PubKey, changeAddress, new Money(7500), new Money(5000));
			var block = WalletTestsHelpers.AppendTransactionInNewBlockToChain(chainInfo.chain, transaction);

			var walletFeePolicy = new Mock<IGeneralPurposeWalletFeePolicy>();
			walletFeePolicy.Setup(w => w.GetMinimumFee(258, 50))
				.Returns(new Money(5000));

			var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chainInfo.chain,
				NodeSettings.Default(),
				dataFolder, walletFeePolicy.Object, new Mock<IAsyncLoopFactory>().Object, new NodeLifetime(),
				DateTimeProvider.Default);
			walletManager.Wallets.Add(wallet);

			walletManager.WalletTipHash = block.Header.GetHash();

			var chainedBlock = chainInfo.chain.GetBlock(block.GetHash());
			walletManager.ProcessBlock(block, chainedBlock);

			var spentAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(0);
			Assert.Equal(1, spendingAddress.Transactions.Count);
			Assert.Equal(transaction.GetHash(), spentAddressResult.Transactions.ElementAt(0).SpendingDetails.TransactionId);
			Assert.Equal(transaction.Outputs[1].Value,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey,
				spentAddressResult.Transactions.ElementAt(0).SpendingDetails.Payments.ElementAt(0).DestinationScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1).Transactions.Count);
			var destinationAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).ExternalAddresses.ElementAt(1)
				.Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), destinationAddressResult.Id);
			Assert.Equal(transaction.Outputs[1].Value, destinationAddressResult.Amount);
			Assert.Equal(transaction.Outputs[1].ScriptPubKey, destinationAddressResult.ScriptPubKey);

			Assert.Equal(1,
				wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0).Transactions.Count);
			var changeAddressResult = wallet.AccountsRoot.ElementAt(0).Accounts.ElementAt(0).InternalAddresses.ElementAt(0)
				.Transactions.ElementAt(0);
			Assert.Equal(transaction.GetHash(), changeAddressResult.Id);
			Assert.Equal(transaction.Outputs[0].Value, changeAddressResult.Amount);
			Assert.Equal(transaction.Outputs[0].ScriptPubKey, changeAddressResult.ScriptPubKey);

			Assert.Equal(chainedBlock.GetLocator().Blocks, wallet.BlockLocator);
			Assert.Equal(chainedBlock.Height, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHeight);
			Assert.Equal(chainedBlock.HashBlock, wallet.AccountsRoot.ElementAt(0).LastBlockSyncedHash);
			Assert.Equal(chainedBlock.HashBlock, walletManager.WalletTipHash);
		}

		[Fact]
		public void ProcessBlockWithWalletTipBlockNotOnChainYetThrowsWalletException()
		{
			Assert.Throws<GeneralPurposeWalletException>(() =>
			{
				DataFolder dataFolder = CreateDataFolder(this);
				Directory.CreateDirectory(dataFolder.WalletPath);

				var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

				ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
				var chainResult = WalletTestsHelpers.AppendBlock(chain.Genesis, chain);

				var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain,
					NodeSettings.Default(),
					dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object,
					new NodeLifetime(), DateTimeProvider.Default);
				walletManager.Wallets.Add(wallet);

				walletManager.WalletTipHash = new uint256(15012522521);

				walletManager.ProcessBlock(chainResult.Block, chainResult.ChainedBlock);
			});
		}

		[Fact]
		public void ProcessBlockWithBlockAheadOfWalletThrowsWalletException()
		{
			Assert.Throws<GeneralPurposeWalletException>(() =>
			{
				DataFolder dataFolder = CreateDataFolder(this);
				Directory.CreateDirectory(dataFolder.WalletPath);

				var wallet = this.walletFixture.GenerateBlankWallet("myWallet1", "password");

				ConcurrentChain chain = new ConcurrentChain(wallet.Network.GetGenesis().Header);
				var chainResult = WalletTestsHelpers.AppendBlock(chain.Genesis, chain);
				var chainResult2 = WalletTestsHelpers.AppendBlock(chainResult.ChainedBlock, chain);

				var walletManager = new GeneralPurposeWalletManager(this.LoggerFactory.Object, Network.Main, chain,
					NodeSettings.Default(),
					dataFolder, new Mock<IGeneralPurposeWalletFeePolicy>().Object, new Mock<IAsyncLoopFactory>().Object,
					new NodeLifetime(), DateTimeProvider.Default);
				walletManager.Wallets.Add(wallet);

				walletManager.WalletTipHash = wallet.Network.GetGenesis().Header.GetHash();

				walletManager.ProcessBlock(chainResult2.Block, chainResult2.ChainedBlock);
			});
		}
	}

	public class GeneralPurposeWalletFixture : IDisposable
    {
        private readonly Dictionary<(string, string), GeneralPurposeWallet> walletsGenerated;

        public GeneralPurposeWalletFixture()
        {
            this.walletsGenerated = new Dictionary<(string, string), GeneralPurposeWallet>();
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
        public GeneralPurposeWallet GenerateBlankWallet(string name, string password)
        {
            if (this.walletsGenerated.TryGetValue((name, password), out GeneralPurposeWallet existingWallet))
            {
                string serializedExistingWallet = JsonConvert.SerializeObject(existingWallet, Formatting.None);
                return JsonConvert.DeserializeObject<GeneralPurposeWallet>(serializedExistingWallet);
            }

	        GeneralPurposeWallet newWallet = WalletTestsHelpers.GenerateBlankWallet(name, password);
            this.walletsGenerated.Add((name, password), newWallet);

            string serializedNewWallet = JsonConvert.SerializeObject(newWallet, Formatting.None);
            return JsonConvert.DeserializeObject<GeneralPurposeWallet>(serializedNewWallet);
        }
    }
}

