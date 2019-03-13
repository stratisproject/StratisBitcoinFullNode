using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Wallet
{
    public class WalletTests
    {
        private const string Password = "password";
        private const string WalletName = "mywallet";
        private const string Passphrase = "passphrase";
        private const string Account = "account 0";
        private readonly Network network;

        public WalletTests()
        {
            this.network = new BitcoinRegTest();
        }

        [Fact]
        public void WalletCanReceiveAndSendCorrectly()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPowNode(this.network).WithWallet().Start();
                CoreNode stratisReceiver = builder.CreateStratisPowNode(this.network).WithWallet().Start();

                int maturity = (int)stratisSender.FullNode.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(stratisSender, maturity + 1 + 5);

                // The mining should add coins to the wallet
                long total = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 6 * 50, total);

                // Sync both nodes
                TestHelper.ConnectAndSync(stratisSender, stratisReceiver);

                // Send coins to the receiver
                HdAddress sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, Account));
                Transaction trx = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(stratisSender.FullNode.Network,
                    new WalletAccountReference(WalletName, Account), Password, sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // Broadcast to the other node
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

                // Wait for the transaction to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

                long receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Generate two new blocks so the transaction is confirmed
                TestHelper.MineBlocks(stratisSender, 2);

                // Wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));

                Assert.Equal(Money.Coins(100), stratisReceiver.FullNode.WalletManager().GetBalances(WalletName, Account).Single().AmountConfirmed);
            }
        }

        [Fact]
        public void WalletCanReorg()
        {
            // This test has 4 parts:
            // Send first transaction from one wallet to another and wait for it to be confirmed
            // Send a second transaction and wait for it to be confirmed
            // Connect to a longer chain that causes a reorg so that the second trasnaction is undone
            // Mine the second transaction back in to the main chain
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPowNode(this.network).WithWallet().Start();
                CoreNode stratisReceiver = builder.CreateStratisPowNode(this.network).WithWallet().Start();
                CoreNode stratisReorg = builder.CreateStratisPowNode(this.network).WithWallet().Start();

                int maturity = (int)stratisSender.FullNode.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(stratisSender, maturity + 1 + 15);

                int currentBestHeight = maturity + 1 + 15;

                // The mining should add coins to the wallet.
                long total = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 16 * 50, total);

                // Sync all nodes.
                TestHelper.ConnectAndSync(stratisReceiver, stratisSender);
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Build Transaction 1.
                // Send coins to the receiver.
                HdAddress sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, Account));
                Transaction transaction1 = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(stratisSender.FullNode.Network, new WalletAccountReference(WalletName, Account), Password, sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // Broadcast to the other node.
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction1.ToHex()));

                // Wait for the transaction to arrive.
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), null, false));
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

                long receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Generate two new blocks so the transaction is confirmed.
                TestHelper.MineBlocks(stratisSender, 1);
                int transaction1MinedHeight = currentBestHeight + 1;
                TestHelper.MineBlocks(stratisSender, 1);
                currentBestHeight = currentBestHeight + 2;

                // Wait for block repo for block sync to work.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => transaction1MinedHeight == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Build Transaction 2.
                // Remove the reorg node.
                TestHelper.Disconnect(stratisReceiver, stratisReorg);
                TestHelper.Disconnect(stratisSender, stratisReorg);

                ChainedHeader forkblock = stratisReceiver.FullNode.Chain.Tip;

                // Send more coins to the wallet
                sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, Account));
                Transaction transaction2 = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(stratisSender.FullNode.Network, new WalletAccountReference(WalletName, Account), Password, sendto.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 101));
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));

                // Wait for the transaction to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction2.GetHash(), null, false));
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
                long newamount = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.Contains(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName), b => b.Transaction.BlockHeight == null);

                // Mine more blocks so it gets included in the chain.
                TestHelper.MineBlocks(stratisSender, 1);
                int transaction2MinedHeight = currentBestHeight + 1;
                TestHelper.MineBlocks(stratisSender, 1);
                currentBestHeight = currentBestHeight + 2;
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));

                // Create a reorg by mining on two different chains.
                // Advance both chains, one chain is longer.
                TestHelper.MineBlocks(stratisSender, 2);
                TestHelper.MineBlocks(stratisReorg, 10);
                currentBestHeight = forkblock.Height + 10;

                // Connect the reorg chain.
                TestHelper.Connect(stratisReceiver, stratisReorg);
                TestHelper.Connect(stratisSender, stratisReorg);

                // Wait for the chains to catch up.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg, true));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);

                // Ensure wallet reorg completes.
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().WalletTipHash == stratisReorg.CreateRPCClient().GetBestBlockHash());

                // Check the wallet amount was rolled back.
                long newtotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(receivetotal, newtotal);
                TestHelper.WaitLoop(() => maturity + 1 + 16 == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // ReBuild Transaction 2.
                // After the reorg transaction2 was returned back to mempool.
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the transaction again.
                TestHelper.MineBlocks(stratisSender, 1);
                transaction2MinedHeight = currentBestHeight + 1;
                TestHelper.MineBlocks(stratisSender, 1);
                currentBestHeight = currentBestHeight + 2;

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));

                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                long newsecondamount = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(newamount, newsecondamount);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));
            }
        }

        [Fact]
        public void Given_TheNodeHadAReorg_And_WalletTipIsBehindConsensusTip_When_ANewBlockArrives_Then_WalletCanRecover()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisReceiver = builder.CreateStratisPowNode(this.network).Start();
                CoreNode stratisReorg = builder.CreateStratisPowNode(this.network).WithWallet().Start();

                // Sync all nodes.
                TestHelper.ConnectAndSync(stratisReceiver, stratisSender);
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Remove the reorg node.
                TestHelper.Disconnect(stratisReceiver, stratisReorg);
                TestHelper.Disconnect(stratisSender, stratisReorg);

                // Create a reorg by mining on two different chains.
                // Advance both chains, one chain is longer.
                TestHelper.MineBlocks(stratisSender, 2);
                TestHelper.MineBlocks(stratisReorg, 10);

                // Rewind the wallet for the stratisReceiver node.
                (stratisReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(5);

                // Connect the reorg chain.
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Wait for the chains to catch up.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(20, stratisReceiver.FullNode.Chain.Tip.Height);

                TestHelper.MineBlocks(stratisSender, 5);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(25, stratisReceiver.FullNode.Chain.Tip.Height);
            }
        }

        [Fact]
        public void Given_TheNodeHadAReorg_And_ConensusTipIsdifferentFromWalletTip_When_ANewBlockArrives_Then_WalletCanRecover()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisSender = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode stratisReceiver = builder.CreateStratisPowNode(this.network).Start();
                CoreNode stratisReorg = builder.CreateStratisPowNode(this.network).WithDummyWallet().Start();

                // Sync all nodes.
                TestHelper.ConnectAndSync(stratisReceiver, stratisSender);
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Remove the reorg node and wait for node to be disconnected.
                TestHelper.Disconnect(stratisReceiver, stratisReorg);
                TestHelper.Disconnect(stratisSender, stratisReorg);

                // Create a reorg by mining on two different chains.
                // Advance both chains, one chain is longer.
                TestHelper.MineBlocks(stratisSender, 2);
                TestHelper.MineBlocks(stratisReorg, 10);

                // Connect the reorg chain.
                TestHelper.ConnectAndSync(stratisReceiver, stratisReorg);
                TestHelper.ConnectAndSync(stratisSender, stratisReorg);

                // Wait for the chains to catch up.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(20, stratisReceiver.FullNode.Chain.Tip.Height);

                // Rewind the wallet in the stratisReceiver node.
                (stratisReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(10);

                TestHelper.MineBlocks(stratisSender, 5);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(25, stratisReceiver.FullNode.Chain.Tip.Height);
            }
        }

        [Fact]
        public void WalletCanCatchupWithBestChain()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisminer = builder.CreateStratisPowNode(this.network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();

                // Push the wallet back.
                stratisminer.FullNode.Services.ServiceProvider.GetService<IWalletSyncManager>().SyncFromHeight(5);

                TestHelper.MineBlocks(stratisminer, 5);
            }
        }

        [Fact(Skip = "Investigate PeerConnector shutdown timeout issue")]
        public void WalletCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).WithWallet().Start();

                TestHelper.MineBlocks(stratisNodeSync, 10);

                // Set the tip of best chain some blocks in the past
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // Stop the node (it will persist the chain with the reset tip)
                stratisNodeSync.FullNode.Dispose();

                CoreNode newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // Load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // Check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.WalletManager().WalletTipHash);
            }
        }

        public static TransactionBuildContext CreateContext(Network network, WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(network)
            {
                AccountReference = accountReference,
                MinConfirmations = minConfirmations,
                FeeType = feeType,
                WalletPassword = password,
                Recipients = new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList()
            };
        }

        /// <summary>
        /// Copies the test wallet into data folder for node if it isn't already present.
        /// </summary>
        /// <param name="path">The path of the folder to move the wallet to.</param>
        private void InitializeTestWallet(string path)
        {
            string testWalletPath = Path.Combine(path, "test.wallet.json");
            if (!File.Exists(testWalletPath))
                File.Copy("Data/test.wallet.json", testWalletPath);
        }
    }
}