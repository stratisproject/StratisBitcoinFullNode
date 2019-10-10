using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpFunctionalExtensions;
using Microsoft.AspNetCore.Mvc;
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
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities.JsonErrors;
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
                stratisSender.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));

                // Wait for the transaction to arrive
                TestBase.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestBase.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

                long receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Generate two new blocks so the transaction is confirmed
                TestHelper.MineBlocks(stratisSender, 2);

                // Wait for block repo for block sync to work
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));

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
                stratisSender.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(transaction1.ToHex()));

                // Wait for the transaction to arrive.
                TestBase.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), null, false));
                TestBase.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());

                long receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Generate two new blocks so the transaction is confirmed.
                TestHelper.MineBlocks(stratisSender, 1);
                int transaction1MinedHeight = currentBestHeight + 1;
                TestHelper.MineBlocks(stratisSender, 1);
                currentBestHeight = currentBestHeight + 2;

                // Wait for block repo for block sync to work.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.ChainIndexer.Tip.Height);
                TestBase.WaitLoop(() => transaction1MinedHeight == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // Build Transaction 2.
                // Remove the reorg node.
                TestHelper.Disconnect(stratisReceiver, stratisReorg);
                TestHelper.Disconnect(stratisSender, stratisReorg);

                ChainedHeader forkblock = stratisReceiver.FullNode.ChainIndexer.Tip;

                // Send more coins to the wallet
                sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, Account));
                Transaction transaction2 = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(stratisSender.FullNode.Network, new WalletAccountReference(WalletName, Account), Password, sendto.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 101));
                stratisSender.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));

                // Wait for the transaction to arrive
                TestBase.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction2.GetHash(), null, false));
                TestBase.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any());
                long newamount = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.Contains(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName), b => b.Transaction.BlockHeight == null);

                // Mine more blocks so it gets included in the chain.
                TestHelper.MineBlocks(stratisSender, 1);
                int transaction2MinedHeight = currentBestHeight + 1;
                TestHelper.MineBlocks(stratisSender, 1);
                currentBestHeight = currentBestHeight + 2;
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.ChainIndexer.Tip.Height);
                TestBase.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));

                // Create a reorg by mining on two different chains.
                // Advance both chains, one chain is longer.
                TestHelper.MineBlocks(stratisSender, 2);
                TestHelper.MineBlocks(stratisReorg, 10);
                currentBestHeight = forkblock.Height + 10;

                // Connect the reorg chain.
                TestHelper.Connect(stratisReceiver, stratisReorg);
                TestHelper.Connect(stratisSender, stratisReorg);

                // Wait for the chains to catch up.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg, true));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.ChainIndexer.Tip.Height);

                // Ensure wallet reorg completes.
                TestBase.WaitLoop(() => stratisReceiver.FullNode.WalletManager().WalletTipHash == stratisReorg.CreateRPCClient().GetBestBlockHash());

                // Check the wallet amount was rolled back.
                long newtotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(receivetotal, newtotal);
                TestBase.WaitLoop(() => maturity + 1 + 16 == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).First().Transaction.BlockHeight);

                // ReBuild Transaction 2.
                // After the reorg transaction2 was returned back to mempool.
                stratisSender.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));
                TestBase.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);

                // Mine the transaction again.
                TestHelper.MineBlocks(stratisSender, 1);
                transaction2MinedHeight = currentBestHeight + 1;
                TestHelper.MineBlocks(stratisSender, 1);
                currentBestHeight = currentBestHeight + 2;

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));

                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.ChainIndexer.Tip.Height);
                long newsecondamount = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(newamount, newsecondamount);
                TestBase.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));
            }
        }

        [Fact]
        public void BuildTransaction_From_ManyUtxos_EnoughFundsForFee()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPowNode(this.network).WithWallet().Start();
                CoreNode node2 = builder.CreateStratisPowNode(this.network).WithWallet().Start();

                int maturity = (int) node1.FullNode.Network.Consensus.CoinbaseMaturity;
                TestHelper.MineBlocks(node1, maturity + 1 + 15);

                int currentBestHeight = maturity + 1 + 15;

                // The mining should add coins to the wallet.
                long total = node1.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 16 * 50, total);

                // Sync all nodes.
                TestHelper.ConnectAndSync(node1, node2);

                const int utxosToSend = 500;
                const int howManyTimes = 8;

                for (int i = 0; i < howManyTimes; i++)
                {
                    HdAddress sendto = node2.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(WalletName, Account));
                    SendManyUtxosTransaction(node1, sendto.ScriptPubKey, Money.FromUnit(907700, MoneyUnit.Satoshi), utxosToSend);
                }

                TestBase.WaitLoop(() => node1.CreateRPCClient().GetRawMempool().Length == howManyTimes);
                TestHelper.MineBlocks(node1, 1);
                TestHelper.WaitForNodeToSync(node1, node2);

                var transactionsToSpend = node2.FullNode.WalletManager().GetSpendableTransactionsInWallet(WalletName);
                Assert.Equal(utxosToSend * howManyTimes, transactionsToSpend.Count());

                // Firstly, build a tx with value 1. Previously this would fail as the WalletTransactionHandler didn't pass enough UTXOs.
                IActionResult result = node2.FullNode.NodeController<WalletController>().BuildTransaction(
                    new BuildTransactionRequest
                    {
                        WalletName = WalletName,
                        AccountName = "account 0",
                        FeeAmount = "0.1",
                        Password = Password,
                        Recipients = new List<RecipientModel>
                        {
                            new RecipientModel
                            {
                                Amount = "1",
                                DestinationAddress = node1.FullNode.WalletManager()
                                    .GetUnusedAddress(new WalletAccountReference(WalletName, Account)).Address
                            }
                        }
                    });

                JsonResult jsonResult = (JsonResult) result;
                Assert.NotNull(((WalletBuildTransactionModel)jsonResult.Value).TransactionId);
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
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(20, stratisReceiver.FullNode.ChainIndexer.Tip.Height);

                TestHelper.MineBlocks(stratisSender, 5);

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(25, stratisReceiver.FullNode.ChainIndexer.Tip.Height);
            }
        }

        [Fact]
        public void Given_TheNodeHadAReorg_And_ConsensusTipIsdifferentFromWalletTip_When_ANewBlockArrives_Then_WalletCanRecover()
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
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(20, stratisReceiver.FullNode.ChainIndexer.Tip.Height);

                // Rewind the wallet in the stratisReceiver node.
                (stratisReceiver.FullNode.NodeService<IWalletSyncManager>() as WalletSyncManager).SyncFromHeight(10);

                TestHelper.MineBlocks(stratisSender, 5);

                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(25, stratisReceiver.FullNode.ChainIndexer.Tip.Height);
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
                stratisNodeSync.FullNode.ChainIndexer.SetTip(stratisNodeSync.FullNode.ChainIndexer.GetHeader(stratisNodeSync.FullNode.ChainIndexer.Height - 5));

                // Stop the node (it will persist the chain with the reset tip)
                stratisNodeSync.FullNode.Dispose();

                CoreNode newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // Load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // Check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.ChainIndexer.Tip.HashBlock, newNodeInstance.FullNode.WalletManager().WalletTipHash);
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

        private static Result<WalletSendTransactionModel> SendManyUtxosTransaction(CoreNode node, Script scriptPubKey, Money amount, int utxos = 1)
        {
            Recipient[] recipients = new Recipient[utxos];
            for (int i = 0; i < recipients.Length; i++)
            {
                recipients[i] = new Recipient { Amount = amount, ScriptPubKey = scriptPubKey };
            }

            var txBuildContext = new TransactionBuildContext(node.FullNode.Network)
            {
                AccountReference = new WalletAccountReference(WalletName, "account 0"),
                MinConfirmations = 1,
                FeeType = FeeType.Medium,
                WalletPassword = Password,
                Recipients = recipients.ToList()
            };

            Transaction trx = (node.FullNode.NodeService<IWalletTransactionHandler>() as IWalletTransactionHandler).BuildTransaction(txBuildContext);

            // Broadcast to the other node.

            IActionResult result = node.FullNode.NodeController<WalletController>().SendTransaction(new SendTransactionRequest(trx.ToHex()));
            if (result is ErrorResult errorResult)
            {
                var errorResponse = (ErrorResponse)errorResult.Value;
                return Result.Fail<WalletSendTransactionModel>(errorResponse.Errors[0].Message);
            }

            JsonResult response = (JsonResult)result;
            return Result.Ok((WalletSendTransactionModel)response.Value);
        }
    }
}