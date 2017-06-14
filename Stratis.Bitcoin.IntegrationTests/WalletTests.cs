using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class WalletTests
    {
        [Fact]
        public void WalletCanReceiveAndSendCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
				var stratisSender = builder.CreateStratisNode();
				var stratisReceiver = builder.CreateStratisNode();

				builder.StartAll();
				stratisSender.NotInIBD();
				stratisReceiver.NotInIBD();

				// get a key from the wallet
	            var mnemonic1 = stratisSender.FullNode.WalletManager.CreateWallet("123456", "mywallet");
	            Assert.Equal(12, mnemonic1.Words.Length);

	            var mnemonic2 = stratisReceiver.FullNode.WalletManager.CreateWallet("123456", "mywallet");
	            Assert.Equal(12, mnemonic2.Words.Length);

				var addr = stratisSender.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
	            var key = stratisSender.FullNode.WalletManager.GetKeyForAddress("123456", addr).PrivateKey;

	            stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
                var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().COINBASE_MATURITY;
                stratisSender.GenerateStratis(maturity + 5);
                // wait for block repo for block sync to work

                Class1.Eventually(() => IsNodeSynced(stratisSender));

                // the mining should add coins to the wallet
                var total = stratisSender.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
				Assert.Equal(Money.COIN * 105 * 50, total);

				// sync both nodes
				stratisSender.CreateRPCClient().AddNode(stratisReceiver.Endpoint, true);
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));

                // send coins to the receiver
                var sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
	            var trx = stratisSender.FullNode.WalletManager.BuildTransaction("mywallet", "account 0", "123456", sendto.Address, Money.COIN * 100, string.Empty, 101);

				// broadcast to the other node
	            stratisSender.FullNode.WalletManager.SendTransaction(trx.hex);

				// wait for the trx to arrive
	            Class1.Eventually(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
	            Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any());

				var receivetotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
	            Assert.Equal(Money.COIN * 100, receivetotal);
	            Assert.Null(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);

				// generate two new blocks do the trx is confirmed
	            stratisSender.GenerateStratis(1, new List<Transaction>(new[] {new Transaction(trx.hex)}));
                stratisSender.GenerateStratis(1);

                // wait for block repo for block sync to work
                Class1.Eventually(() => IsNodeSynced(stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));

                Class1.Eventually(() => maturity + 6 == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);
            }

        }

        [Fact]
        public void WalletCanReorg()
        {
            // this test has 4 parts:
            // send first transaction from one wallet to another and wait for it to be confirmed
            // send a second transaction and wait for it to be confirmed
            // connected to a longer chain that couse a reorg back so the second trasnaction is undone
            // mine the second transaction back in to the main chain

            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisSender = builder.CreateStratisNode();
                var stratisReceiver = builder.CreateStratisNode();
                var stratisReorg = builder.CreateStratisNode();

                builder.StartAll();
                stratisSender.NotInIBD();
                stratisReceiver.NotInIBD();
                stratisReorg.NotInIBD();

                // get a key from the wallet
                var mnemonic1 = stratisSender.FullNode.WalletManager.CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);

                var mnemonic2 = stratisReceiver.FullNode.WalletManager.CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic2.Words.Length);

                var addr = stratisSender.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
                var key = stratisSender.FullNode.WalletManager.GetKeyForAddress("123456", addr).PrivateKey;

                stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
                stratisReorg.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));

                var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().COINBASE_MATURITY;
                stratisSender.GenerateStratis(maturity + 15);
                var currentBestHeight = maturity + 15;

                // wait for block repo for block sync to work
                Class1.Eventually(() => IsNodeSynced(stratisSender));

                // the mining should add coins to the wallet
                var total = stratisSender.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
                Assert.Equal(Money.COIN * currentBestHeight * 50, total);

                // sync all nodes
                stratisReceiver.CreateRPCClient().AddNode(stratisSender.Endpoint, true);
                stratisReceiver.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                stratisSender.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisReorg));

                // Build Transaction 1
                // ====================
                // send coins to the receiver
                var sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
                var transaction1 = stratisSender.FullNode.WalletManager.BuildTransaction("mywallet", "account 0", "123456", sendto.Address, Money.COIN * 100, string.Empty, 101);

                // broadcast to the other node
                stratisSender.FullNode.WalletManager.SendTransaction(transaction1.hex);

                // wait for the trx to arrive
                Class1.Eventually(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction1.transactionId, false));
                Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any());

                var receivetotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);

                // generate two new blocks so the trx is confirmed
                stratisSender.GenerateStratis(1, new List<Transaction>(new[] { new Transaction(transaction1.hex) }));
                var transaction1MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratis(1);
                currentBestHeight = currentBestHeight + 2;

                // wait for block repo for block sync to work
                Class1.Eventually(() => IsNodeSynced(stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                Class1.Eventually(() => transaction1MinedHeight == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);

                // Build Transaction 2
                // ====================
                // remove the reorg node
                stratisReceiver.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                stratisSender.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                var forkblock = stratisReceiver.FullNode.Chain.Tip;

                // send more coins to the wallet
                sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress("mywallet", "account 0");
                var transaction2 = stratisSender.FullNode.WalletManager.BuildTransaction("mywallet", "account 0", "123456", sendto.Address, Money.COIN * 10, string.Empty, 101);
                stratisSender.FullNode.WalletManager.SendTransaction(transaction2.hex);
                // wait for the trx to arrive
                Class1.Eventually(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction2.transactionId, false));
                Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any());
                var newamount = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.True(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any(b => b.BlockHeight == null));

                // mine more blocks so its included in the chain
              
                stratisSender.GenerateStratis(1, new List<Transaction>(new[] { new Transaction(transaction2.hex) }));
                var transaction2MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratis(1);
                currentBestHeight = currentBestHeight + 2;
                Class1.Eventually(() => IsNodeSynced(stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any(b => b.BlockHeight == transaction2MinedHeight));

                // advance both chains, one chin is longer
                stratisSender.GenerateStratis(2);
                stratisReorg.GenerateStratis(10);
                currentBestHeight = forkblock.Height + 10;
                Class1.Eventually(() => IsNodeSynced(stratisSender));
                Class1.Eventually(() => IsNodeSynced(stratisReorg));

                // connect the reorg chain
                stratisReceiver.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                stratisSender.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                // wait for the chains to catch up
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);

                // ensure wallet reorg complete
                Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.LastReceivedBlock == stratisReorg.CreateRPCClient().GetBestBlockHash());
                // check the wallet amont was roled back
                var newtotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
                Assert.Equal(receivetotal, newtotal);
                Class1.Eventually(() => maturity + 16 == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).First().BlockHeight);

                // ReBuild Transaction 2
                // ====================
                // mine the transaction again
                stratisSender.GenerateStratis(1, new List<Transaction>(new[] { new Transaction(transaction2.hex) }));
                transaction2MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratis(1);
                currentBestHeight = currentBestHeight + 2;

                Class1.Eventually(() => IsNodeSynced(stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisSender));
                Class1.Eventually(() => AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                var newsecondamount = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Sum(s => s.Amount);
                Assert.Equal(newamount, newsecondamount);
                Class1.Eventually(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions().SelectMany(s => s.Transactions).Any(b => b.BlockHeight == transaction2MinedHeight));
            }
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2)
        {
            if (node1.FullNode.Chain.Tip.HashBlock != node2.FullNode.Chain.Tip.HashBlock) return false;
            if (node1.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock != node2.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock) return false;
            if (node1.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock != node2.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock) return false;
            if (node1.FullNode.MempoolManager.InfoAll().Count != node2.FullNode.MempoolManager.InfoAll().Count) return false;
            if (node1.FullNode.WalletManager.LastReceivedBlock != node2.FullNode.WalletManager.LastReceivedBlock) return false;
            if (node1.CreateRPCClient().GetBestBlockHash() != node2.CreateRPCClient().GetBestBlockHash()) return false;
            return true;
        }

        public static bool IsNodeSynced(CoreNode node)
        {
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock) return false;
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock) return false;
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.WalletManager.LastReceivedBlock) return false;
            return true;
        }
    }
}
