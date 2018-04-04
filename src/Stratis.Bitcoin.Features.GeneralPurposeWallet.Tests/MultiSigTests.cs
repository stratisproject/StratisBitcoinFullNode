using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Data.Edm.Validation;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.GeneralPurposeWallet.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.WatchOnlyWallet;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Interfaces;
using Xunit;

namespace Stratis.Bitcoin.Features.GeneralPurposeWallet.Tests
{
    public class MultiSigTests
    {
	    private class TestMultiSig
	    {
		    public Key privateKey;
		    public Key alicePrivateKey;
		    public Key bobPrivateKey;

		    public PubKey pubKey;
		    public PubKey alicePubKey;
		    public PubKey bobPubKey;

		    public MultiSigAddress multiSigAddress;

			public TestMultiSig()
		    {
			    this.privateKey = new Key();
			    this.alicePrivateKey = new Key();
			    this.bobPrivateKey = new Key();

			    this.pubKey = this.privateKey.PubKey;
			    this.alicePubKey = this.alicePrivateKey.PubKey;
			    this.bobPubKey = this.bobPrivateKey.PubKey;

			    this.multiSigAddress = new MultiSigAddress();
			    this.multiSigAddress.Create(this.privateKey, new[] { this.pubKey, this.alicePubKey, this.bobPubKey }, 2, Network.RegTest);
			}
	    }

	    private TestMultiSig MultiSigSetup()
	    {
			return new TestMultiSig();
	    }

	    [Fact]
	    public void CanCreateMultiSigAddress()
	    {
		    var multiSig = this.MultiSigSetup();

		    Assert.NotNull(multiSig.multiSigAddress.PrivateKey);
		    Assert.NotNull(multiSig.multiSigAddress.RedeemScript);
		    Assert.NotNull(multiSig.multiSigAddress.ScriptPubKey);
			Assert.NotNull(multiSig.multiSigAddress.Address);
	    }

	    [Fact]
	    public void CanImportMultiSigAddress()
	    {
			var multiSig = this.MultiSigSetup();

			using (NodeBuilder builder = NodeBuilder.Create())
		    {
			    CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
			    {
				    fullNodeBuilder
					    .UsePowConsensus()
					    .UseBlockStore()
					    .UseMempool()
					    .UseBlockNotification()
					    .UseTransactionNotification()
					    .UseWallet()
					    .UseWatchOnlyWallet()
					    .UseGeneralPurposeWallet()
					    .AddMining()
					    //.UseApi()
					    .AddRPC();
			    });

				var gwm1 = node1.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;

			    gwm1.CreateWallet("Multisig1", "multisig");

				var gwallet1 = gwm1.GetWalletByName("multisig");
				var gaccount1 = gwallet1.GetAccountsByCoinType((Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType)node1.FullNode.Network.Consensus.CoinType).First();

				gaccount1.ImportMultiSigAddress(multiSig.multiSigAddress);

				Assert.NotEmpty(gaccount1.MultiSigAddresses);
		    }
		}

	    [Fact]
	    public void CanReceiveFundsForMultiSigAddress()
	    {
		    var multiSig = this.MultiSigSetup();

			// Start up node & mine a chain

			using (NodeBuilder builder = NodeBuilder.Create())
			{
			    CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
			    {
				    fullNodeBuilder
					    .UsePowConsensus()
					    .UseBlockStore()
					    .UseMempool()
						.UseBlockNotification()
						.UseTransactionNotification()
					    .UseWallet()
					    .UseWatchOnlyWallet()
						.UseGeneralPurposeWallet()
					    .AddMining()
					    //.UseApi()
					    .AddRPC();
			    });

			    var rpc1 = node1.CreateRPCClient();

			    // Create the originating node's wallet

			    var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
			    wm1.CreateWallet("Multisig1", "multisig");

				var wth1 = node1.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;

				var bm1 = node1.FullNode.NodeService<IBroadcasterManager>() as FullNodeBroadcasterManager;

				var wallet1 = wm1.GetWalletByName("multisig");
			    var account1 = wallet1.GetAccountsByCoinType((Wallet.CoinType) node1.FullNode.Network.Consensus.CoinType).First();
			    var address1 = account1.GetFirstUnusedReceivingAddress();
			    var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Multisig1", address1);

			    // We can use SetDummyMinerSecret here because the private key is already in the wallet
			    node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

				// Create the originating node's wallet
				var gwm1 = node1.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;
				gwm1.CreateWallet("Multisig1", "multisig");

				var gwallet1 = gwm1.GetWalletByName("multisig");
				var gaccount1 = gwallet1.GetAccountsByCoinType((Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType)node1.FullNode.Network.Consensus.CoinType).First();

				gaccount1.ImportMultiSigAddress(multiSig.multiSigAddress);

				// Generate blocks so we have some funds to create a transaction with
				rpc1.Generate(102);

				wm1.SaveWallets();

				// Send funds to multiSigAddress.Address
				var destination = BitcoinAddress.Create(multiSig.multiSigAddress.Address, Network.RegTest).ScriptPubKey;

				var context = new Wallet.TransactionBuildContext(
					new WalletAccountReference("multisig", "account 0"),
					new[] { new Wallet.Recipient { Amount = Money.Coins(10m), ScriptPubKey = destination } }.ToList(),
					"Multisig1")
				{
					TransactionFee = Money.Coins(0.01m),
					MinConfirmations = 101, // The wallet does not appear to regard immature coinbases as unspendable
					Shuffle = true
				};

				var transactionResult = wth1.BuildTransaction(context);

				bm1.BroadcastTransactionAsync(transactionResult).GetAwaiter().GetResult();
				
				// Note that the wallet manager's ProcessTransaction gets initially called when the transaction
				// first appears (e.g. got relayed?), not necessarily in a block. It then gets called later
				// when the transaction does appear in a block

				// Mine a block

				rpc1.Generate(1);

				wm1.SaveWallets();

				// Check that the transaction list for the multisig address contains the incoming transaction

				Assert.NotEmpty(gaccount1.MultiSigAddresses.First().Transactions);

				var receivedTx = gaccount1.MultiSigAddresses.First().Transactions.First();
				Assert.NotNull(receivedTx.Transaction);

				// As an extra block was mined, these should all now be populated by ProcessTransaction

				Assert.NotNull(receivedTx.BlockHeight);
				Assert.NotNull(receivedTx.BlockHash);
				Assert.NotNull(receivedTx.MerkleProof);
			}
		}

		[Fact]
		public void CanGetBalanceForMultiSigAddress()
		{
			var multiSig = this.MultiSigSetup();

			// Start up node & mine a chain

			using (NodeBuilder builder = NodeBuilder.Create())
			{
				CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
				{
					fullNodeBuilder
						.UsePowConsensus()
						.UseBlockStore()
						.UseMempool()
						.UseBlockNotification()
						.UseTransactionNotification()
						.UseWallet()
						.UseWatchOnlyWallet()
						.UseGeneralPurposeWallet()
						.AddMining()
						//.UseApi()
						.AddRPC();
				});

				var rpc1 = node1.CreateRPCClient();

				// Create the originating node's wallet

				var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
				wm1.CreateWallet("Multisig1", "multisig");

				var wth1 = node1.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;

				var bm1 = node1.FullNode.NodeService<IBroadcasterManager>() as FullNodeBroadcasterManager;

				var wallet1 = wm1.GetWalletByName("multisig");
				var account1 = wallet1.GetAccountsByCoinType((Wallet.CoinType)node1.FullNode.Network.Consensus.CoinType).First();
				var address1 = account1.GetFirstUnusedReceivingAddress();
				var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Multisig1", address1);

				// We can use SetDummyMinerSecret here because the private key is already in the wallet
				node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

				// Create the originating node's wallet
				var gwm1 = node1.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;
				gwm1.CreateWallet("Multisig1", "multisig");

				var gwallet1 = gwm1.GetWalletByName("multisig");
				var gaccount1 = gwallet1.GetAccountsByCoinType((Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType)node1.FullNode.Network.Consensus.CoinType).First();

				gaccount1.ImportMultiSigAddress(multiSig.multiSigAddress);

				// Generate blocks so we have some funds to create a transaction with
				rpc1.Generate(102);

				wm1.SaveWallets();

				// Send funds to multiSigAddress.Address
				var destination = BitcoinAddress.Create(multiSig.multiSigAddress.Address, Network.RegTest).ScriptPubKey;

				var context = new Wallet.TransactionBuildContext(
					new WalletAccountReference("multisig", "account 0"),
					new[] { new Wallet.Recipient { Amount = Money.Coins(10m), ScriptPubKey = destination } }.ToList(),
					"Multisig1")
				{
					TransactionFee = Money.Coins(0.01m),
					MinConfirmations = 101, // The wallet does not appear to regard immature coinbases as unspendable
					Shuffle = true
				};

				var transactionResult = wth1.BuildTransaction(context);

				bm1.BroadcastTransactionAsync(transactionResult).GetAwaiter().GetResult();

				// Note that the wallet manager's ProcessTransaction gets initially called when the transaction
				// first appears (e.g. got relayed?), not necessarily in a block. It then gets called later
				// when the transaction does appear in a block

				// Mine a block

				rpc1.Generate(1);

				wm1.SaveWallets();

				var receivedTx = gaccount1.MultiSigAddresses.First().Transactions.First();

				// Check that the spendable amount for the entire multisig wallet can be retrieved & is accurate
				(var conf, var unConf) = gaccount1.GetSpendableAmount(true);

				Assert.Equal(receivedTx.Amount, (conf + unConf));

				// Now check that the spendable amount can be directly obtained for a particular address
				(var conf2, var unConf2) = gaccount1.GetMultiSigAddressSpendableAmount(multiSig.multiSigAddress.Address);

				Assert.Equal(receivedTx.Amount, (conf2 + unConf2));
			}
		}
		
		[Fact]
	    public void CanSignMultiSigTransaction()
	    {
			// TODO: The logic in this test should probably be incorporated into the wallet itself

			var multiSig = this.MultiSigSetup();

			// Create arbitrary coin so that we don't introduce the complexity of a node running and mining
			ScriptCoin coin = new ScriptCoin(
			    new OutPoint(uint256.One, 0),
			    new TxOut(Money.Coins(100m), multiSig.multiSigAddress.ScriptPubKey),
			    multiSig.multiSigAddress.RedeemScript
		    );

		    TransactionBuilder txBuilderUnsigned = new TransactionBuilder();

			Transaction unsigned =
			    txBuilderUnsigned
				    .AddCoins(coin)
				    .Send(new Key().PubKey.GetAddress(Network.RegTest), Money.Coins(5.0m))
				    .SetChange(multiSig.multiSigAddress.ScriptPubKey)
				    .BuildTransaction(sign: false);

			// Now emulate the partial transaction being signed by enough signatories.

			// In practice the builders will be operating on different nodes, so use separate ones
			// for each signatory.

			// First signatory
		    TransactionBuilder txBuilder = new TransactionBuilder();

			Transaction partial =
			    txBuilder
				    .AddCoins(coin)
				    .AddKeys(multiSig.privateKey)
				    .SignTransaction(unsigned);

			// Second signatory
		    TransactionBuilder aliceTxBuilder = new TransactionBuilder();

		    Transaction alicePartial =
			    aliceTxBuilder
				    .AddCoins(coin)
				    .AddKeys(multiSig.alicePrivateKey)
				    .SignTransaction(unsigned);

			// Now combine the signatures

		    TransactionBuilder fullySignedTxBuilder = new TransactionBuilder();

			Transaction fullySigned =
				fullySignedTxBuilder
					.AddCoins(coin)
				    .CombineSignatures(new [] {partial, alicePartial});

			Assert.NotNull(fullySigned);
		}

	    [Fact]
	    public void CanFundMultiSigTransaction()
	    {
			// This will test the functionality of a modified TransactionBuilder.
			// It should be able to enumerate the unspent outputs in the
			// MultiSigAddress transactions list and construct a suitable transaction
			// for signing by the signatories.

		    var multiSig = this.MultiSigSetup();

			// Start up node & mine a chain

			using (NodeBuilder builder = NodeBuilder.Create())
			{
				CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
				{
					fullNodeBuilder
						.UsePowConsensus()
						.UseBlockStore()
						.UseMempool()
						.UseBlockNotification()
						.UseTransactionNotification()
						.UseWallet()
						.UseWatchOnlyWallet()
						.UseGeneralPurposeWallet()
						.AddMining()
						//.UseApi()
						.AddRPC();
				});

				var rpc1 = node1.CreateRPCClient();

				// Create the originating node's wallet

				var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
				wm1.CreateWallet("Multisig1", "multisig");

				var wth1 = node1.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;

				var bm1 = node1.FullNode.NodeService<IBroadcasterManager>() as FullNodeBroadcasterManager;

				var wallet1 = wm1.GetWalletByName("multisig");
				var account1 = wallet1.GetAccountsByCoinType((Wallet.CoinType)node1.FullNode.Network.Consensus.CoinType).First();
				var address1 = account1.GetFirstUnusedReceivingAddress();
				var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Multisig1", address1);

				// We can use SetDummyMinerSecret here because the private key is already in the wallet
				node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

				// Create the originating node's wallet
				var gwm1 = node1.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;
				gwm1.CreateWallet("Multisig1", "multisig");

				var gwallet1 = gwm1.GetWalletByName("multisig");
				var gaccount1 = gwallet1.GetAccountsByCoinType((Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType)node1.FullNode.Network.Consensus.CoinType).First();

				gaccount1.ImportMultiSigAddress(multiSig.multiSigAddress);

				var gwth1 = node1.FullNode.NodeService<IGeneralPurposeWalletTransactionHandler>() as GeneralPurposeWalletTransactionHandler;

				// Generate blocks so we have some funds to create a transaction with
				rpc1.Generate(102);

				wm1.SaveWallets();

				// Send funds to multiSigAddress.Address
				var destination = BitcoinAddress.Create(multiSig.multiSigAddress.Address, Network.RegTest).ScriptPubKey;

				var context = new Wallet.TransactionBuildContext(
					new WalletAccountReference("multisig", "account 0"),
					new[] { new Wallet.Recipient { Amount = Money.Coins(10m), ScriptPubKey = destination } }.ToList(),
					"Multisig1")
				{
					TransactionFee = Money.Coins(0.01m),
					MinConfirmations = 101, // The wallet does not appear to regard immature coinbases as unspendable
					Shuffle = true
				};

				var transactionResult = wth1.BuildTransaction(context);

				bm1.BroadcastTransactionAsync(transactionResult).GetAwaiter().GetResult();

				// Note that the wallet manager's ProcessTransaction gets initially called when the transaction
				// first appears (e.g. got relayed or put into mempool?), not necessarily in a block. It then gets
				// called later when the transaction does appear in a block

				// Mine a block

				rpc1.Generate(1);

				// We now presume that we have funds available in the multisig address (tested in another test).
				// Now try to build a multisig partial transaction using the funds in the address.
				// We will obviously only be able to sign one of the signatures.

				// Send funds to an arbitrary address
				//var arbitraryDestination = BitcoinAddress.Create("mm9gQ2aULVkLiJrWTkGLfwBDvCYPK8Di1q", Network.RegTest).ScriptPubKey;
				var arbitraryDestination = gaccount1.ExternalAddresses.First().ScriptPubKey;

				var multiSigContext = new Stratis.Bitcoin.Features.GeneralPurposeWallet.TransactionBuildContext(
					new GeneralPurposeWalletAccountReference("multisig", "account 0"),
					new[] { new Stratis.Bitcoin.Features.GeneralPurposeWallet.Recipient { Amount = Money.Coins(5m), ScriptPubKey = arbitraryDestination } }.ToList(),
					"Multisig1")
				{
					TransactionFee = Money.Coins(0.01m),
					MinConfirmations = 1, // The funds in the multisig address have just been confirmed
					Shuffle = true,
					MultiSig = multiSig.multiSigAddress,
					IgnoreVerify = true
				};

				var multiSigTransactionResult = gwth1.BuildTransaction(multiSigContext);

				// Sign partial transaction above with one of the other members' keys

				TransactionBuilder aliceTxBuilder = new TransactionBuilder();

				// Here we make the assumption that the other federation member would have identical wallet contents,
				// and can therefore construct a similar ScriptCoin for the builder to use

				var coins = multiSig.multiSigAddress.Transactions.First().Transaction.Outputs.AsCoins();

				Transaction aliceSigned =
					aliceTxBuilder
						.AddCoins(coins)
						.AddKeys(multiSig.alicePrivateKey)
						.SignTransaction(multiSigTransactionResult);

				// Check that transaction was built successfully
				
				// Use the HD wallet's broadcast manager, as injecting it into the general purpose wallet appears to cause conflicts
				// TODO: This will be problematic if the user elects to build a node with only the general purpose wallet added
				bm1.BroadcastTransactionAsync(aliceSigned).GetAwaiter().GetResult();

				// Check that spending details for the input funds to the multisig transaction exist

				rpc1.Generate(1);

				Assert.NotEmpty(gaccount1.ExternalAddresses.First().Transactions);

				var destTx = gaccount1.ExternalAddresses.First().Transactions.First();

				// Check that the multisig address has a transaction in it with populated spending details.
				// This must be the same transaction present in the first ExternalAddress as that is the
				// destination of the multisig transaction.
				bool found = false;
				foreach (var tx in gaccount1.MultiSigAddresses.First().Transactions)
				{
					if (tx.SpendingDetails != null)
					{
						found = true;
						Assert.Equal(tx.SpendingDetails.TransactionId, destTx.Id);
					}
				}

				Assert.True(found);

				// Check change output came back to multisig address. This is the same ID as destTx above.
				Assert.NotNull(gaccount1.MultiSigAddresses.First().Transactions.FirstOrDefault(i => i.Id == destTx.Id));
			}
		}

	    [Fact]
	    public void CanCombinePartialTransactions()
	    {
		    var multiSig = this.MultiSigSetup();

		    using (NodeBuilder builder = NodeBuilder.Create())
		    {
			    CoreNode node1 = builder.CreateStratisPowNode(true, fullNodeBuilder =>
			    {
				    fullNodeBuilder
					    .UsePowConsensus()
					    .UseBlockStore()
					    .UseMempool()
					    .UseBlockNotification()
					    .UseTransactionNotification()
					    .UseWallet()
					    .UseWatchOnlyWallet()
					    .UseGeneralPurposeWallet()
					    .AddMining()
					    //.UseApi()
					    .AddRPC();
			    });

			    var rpc1 = node1.CreateRPCClient();

			    var wm1 = node1.FullNode.NodeService<IWalletManager>() as WalletManager;
			    wm1.CreateWallet("Multisig1", "multisig");

			    var wth1 = node1.FullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;

			    var bm1 = node1.FullNode.NodeService<IBroadcasterManager>() as FullNodeBroadcasterManager;

			    var wallet1 = wm1.GetWalletByName("multisig");
			    var account1 = wallet1.GetAccountsByCoinType((Wallet.CoinType) node1.FullNode.Network.Consensus.CoinType)
				    .First();
			    var address1 = account1.GetFirstUnusedReceivingAddress();
			    var secret1 = wallet1.GetExtendedPrivateKeyForAddress("Multisig1", address1);

			    node1.SetDummyMinerSecret(new BitcoinSecret(secret1.PrivateKey, node1.FullNode.Network));

			    var gwm1 = node1.FullNode.NodeService<IGeneralPurposeWalletManager>() as GeneralPurposeWalletManager;
			    gwm1.CreateWallet("Multisig1", "multisig");

			    var gwallet1 = gwm1.GetWalletByName("multisig");
			    var gaccount1 = gwallet1
				    .GetAccountsByCoinType(
					    (Stratis.Bitcoin.Features.GeneralPurposeWallet.CoinType) node1.FullNode.Network.Consensus.CoinType).First();

			    gaccount1.ImportMultiSigAddress(multiSig.multiSigAddress);

			    var gwth1 =
				    node1.FullNode.NodeService<IGeneralPurposeWalletTransactionHandler>() as GeneralPurposeWalletTransactionHandler;

			    rpc1.Generate(102);

			    wm1.SaveWallets();

			    var destination = BitcoinAddress.Create(multiSig.multiSigAddress.Address, Network.RegTest).ScriptPubKey;

			    var context = new Wallet.TransactionBuildContext(
				    new WalletAccountReference("multisig", "account 0"),
				    new[] {new Wallet.Recipient {Amount = Money.Coins(10m), ScriptPubKey = destination}}.ToList(),
				    "Multisig1")
			    {
				    TransactionFee = Money.Coins(0.01m),
				    MinConfirmations = 101, // The wallet does not appear to regard immature coinbases as unspendable
				    Shuffle = true
			    };

			    var transactionResult = wth1.BuildTransaction(context);

			    bm1.BroadcastTransactionAsync(transactionResult).GetAwaiter().GetResult();

			    rpc1.Generate(1);

			    var arbitraryDestination = gaccount1.ExternalAddresses.First().ScriptPubKey;

			    var multiSigContext = new Stratis.Bitcoin.Features.GeneralPurposeWallet.TransactionBuildContext(
				    new GeneralPurposeWalletAccountReference("multisig", "account 0"),
				    new[]
				    {
					    new Stratis.Bitcoin.Features.GeneralPurposeWallet.Recipient
					    {
						    Amount = Money.Coins(5m),
						    ScriptPubKey = arbitraryDestination
					    }
				    }.ToList(),
				    "Multisig1")
			    {
				    TransactionFee = Money.Coins(0.01m),
				    MinConfirmations = 1, // The funds in the multisig address have just been confirmed
				    Shuffle = true,
				    MultiSig = multiSig.multiSigAddress,
				    IgnoreVerify = true,
					Sign = false
			    };

			    var multiSigTransactionResult = gwth1.BuildTransaction(multiSigContext);

			    List<ScriptCoin> coins = new List<ScriptCoin>();

			    foreach (var tempCoin in multiSig.multiSigAddress.Transactions.First().Transaction.Outputs.AsCoins())
			    {
					if (tempCoin.ScriptPubKey == multiSig.multiSigAddress.ScriptPubKey)
						coins.Add(tempCoin.ToScriptCoin(multiSig.multiSigAddress.RedeemScript));
			    }

				// Sign the built unsigned transaction with first private key

				TransactionBuilder txBuilder = new TransactionBuilder();

			    Transaction signed =
				    txBuilder
					    .AddCoins(coins)
					    .AddKeys(multiSig.privateKey)
					    .SignTransaction(multiSigTransactionResult);

				// Sign a copy of the built unsigned transaction with a second private key

				TransactionBuilder aliceTxBuilder = new TransactionBuilder();

			    Transaction aliceSigned =
				    aliceTxBuilder
					    .AddCoins(coins)
					    .AddKeys(multiSig.alicePrivateKey)
					    .SignTransaction(multiSigTransactionResult);

			    Transaction final = gaccount1.CombinePartialTransactions(new Transaction[] {signed, aliceSigned});

				Assert.NotNull(final);

			    bm1.BroadcastTransactionAsync(final).GetAwaiter().GetResult();

			    // Check that spending details for the input funds to the multisig transaction exist

			    rpc1.Generate(1);

			    Assert.NotEmpty(gaccount1.ExternalAddresses.First().Transactions);

			    var destTx = gaccount1.ExternalAddresses.First().Transactions.First();

			    // Check that the multisig address has a transaction in it with populated spending details.
			    // This must be the same transaction present in the first ExternalAddress as that is the
			    // destination of the multisig transaction.
			    bool found = false;
			    foreach (var tx in gaccount1.MultiSigAddresses.First().Transactions)
			    {
				    if (tx.SpendingDetails != null)
				    {
					    found = true;
					    Assert.Equal(tx.SpendingDetails.TransactionId, destTx.Id);
				    }
			    }

			    Assert.True(found);

			    // Check change output came back to multisig address. This is the same ID as destTx above.
			    Assert.NotNull(gaccount1.MultiSigAddresses.First().Transactions.FirstOrDefault(i => i.Id == destTx.Id));
			}
	    }
    }
}
