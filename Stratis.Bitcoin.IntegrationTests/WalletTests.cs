using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using System.Collections.Generic;
using System.Linq;
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
                var mnemonic2 = stratisReceiver.FullNode.WalletManager.CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = stratisSender.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var key = stratisSender.FullNode.WalletManager.GetKeyForAddress("mywallet","123456", addr).PrivateKey;

                stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
                var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().COINBASE_MATURITY;
                stratisSender.GenerateStratis(maturity + 5);
                // wait for block repo for block sync to work

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));

                // the mining should add coins to the wallet
                var total = stratisSender.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 105 * 50, total);

                // sync both nodes
                stratisSender.CreateRPCClient().AddNode(stratisReceiver.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));

                // send coins to the receiver
                var sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var trx = stratisSender.FullNode.WalletTransactionHandler.BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // broadcast to the other node
                stratisSender.FullNode.WalletManager.SendTransaction(trx.ToHex());

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Any());

                var receivetotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).First().Transaction.BlockHeight);

                // generate two new blocks do the trx is confirmed
                stratisSender.GenerateStratis(1, new List<Transaction>(new[] {trx.Clone()}));
                stratisSender.GenerateStratis(1);

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));

                TestHelper.WaitLoop(() => maturity + 6 == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).First().Transaction.BlockHeight);
            }

        }

        [Fact]
        public void CanMineBlocks()
        {
            using(NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisNode();
                builder.StartAll();
                var rpc = stratisNodeSync.CreateRPCClient();
                rpc.SendCommand(NBitcoin.RPC.RPCOperations.generate, 10);
                Assert.Equal(10, rpc.GetBlockCount());
            }
        }

        [Fact]
        public void CanSendToAddress()
        {
            using(NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisNode();
                builder.StartAll();
                var rpc = stratisNodeSync.CreateRPCClient();
                rpc.SendCommand(NBitcoin.RPC.RPCOperations.generate, 101);
                var address = new Key().PubKey.GetAddress(rpc.Network);
                var tx = rpc.SendToAddress(address, Money.Coins(1.0m));
                Assert.NotNull(tx);
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
                var mnemonic2 = stratisReceiver.FullNode.WalletManager.CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = stratisSender.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var key = stratisSender.FullNode.WalletManager.GetKeyForAddress("mywallet","123456", addr).PrivateKey;

                stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
                stratisReorg.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));

                var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().COINBASE_MATURITY;
                stratisSender.GenerateStratisWithMiner(maturity + 15);

                var currentBestHeight = maturity + 15;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));

                // the mining should add coins to the wallet
                var total = stratisSender.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * currentBestHeight * 50, total);

                // sync all nodes
                stratisReceiver.CreateRPCClient().AddNode(stratisSender.Endpoint, true);
                stratisReceiver.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                stratisSender.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));

                // Build Transaction 1
                // ====================
                // send coins to the receiver
                var sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction1 = stratisSender.FullNode.WalletTransactionHandler.BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // broadcast to the other node
                stratisSender.FullNode.WalletManager.SendTransaction(transaction1.ToHex());

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), false));
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Any());

                var receivetotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).First().Transaction.BlockHeight);

                // generate two new blocks so the trx is confirmed
                stratisSender.GenerateStratisWithMiner(1);
                var transaction1MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratisWithMiner(1);
                currentBestHeight = currentBestHeight + 2;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => transaction1MinedHeight == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).First().Transaction.BlockHeight);

                // Build Transaction 2
                // ====================
                // remove the reorg node
                stratisReceiver.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                stratisSender.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                var forkblock = stratisReceiver.FullNode.Chain.Tip;

                // send more coins to the wallet
                sendto = stratisReceiver.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction2 = stratisSender.FullNode.WalletTransactionHandler.BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 101));
                stratisSender.FullNode.WalletManager.SendTransaction(transaction2.ToHex());
                // wait for the trx to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction2.GetHash(), false));
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Any());
                var newamount = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.True(stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Any(b => b.Transaction.BlockHeight == null));

                // mine more blocks so its included in the chain
              
                stratisSender.GenerateStratisWithMiner(1);
                var transaction2MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratisWithMiner(1);
                currentBestHeight = currentBestHeight + 2;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));

                // create a reorg by mining on two different chains
                // ================================================
                // advance both chains, one chin is longer
                stratisSender.GenerateStratisWithMiner(2);
                stratisReorg.GenerateStratisWithMiner(10);
                currentBestHeight = forkblock.Height + 10;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisReorg));

                // connect the reorg chain
                stratisReceiver.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                stratisSender.CreateRPCClient().AddNode(stratisReorg.Endpoint, true);
                // wait for the chains to catch up
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);

                // ensure wallet reorg complete
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager.WalletTipHash == stratisReorg.CreateRPCClient().GetBestBlockHash());
                // check the wallet amount was rolled back
                var newtotal = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(receivetotal, newtotal);
                TestHelper.WaitLoop(() => maturity + 16 == stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).First().Transaction.BlockHeight);

                // ReBuild Transaction 2
                // ====================
                // TODO: Investigate when a reorg happens the store needs to put the rolled back transactions back to the mempool
                stratisSender.FullNode.WalletManager.SendTransaction(transaction2.ToHex());
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);

                // mine the transaction again
                stratisSender.GenerateStratisWithMiner(1);
                transaction2MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratisWithMiner(1);
                currentBestHeight = currentBestHeight + 2;

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisReorg));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                var newsecondamount = stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Sum(s => s.Transaction.Amount);
                Assert.Equal(newamount, newsecondamount);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager.GetSpendableTransactions("mywallet").SelectMany(s => s.UnspentOutputs).Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));
            }
        }

        [Fact]
        public void WalletCanCatchupWithBestChain()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisminer = builder.CreateStratisNode();

                builder.StartAll();
                stratisminer.NotInIBD();

                // get a key from the wallet
                var mnemonic = stratisminer.FullNode.WalletManager.CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic.Words.Length);
                var addr = stratisminer.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var key = stratisminer.FullNode.WalletManager.GetKeyForAddress("mywallet", "123456", addr).PrivateKey;

                stratisminer.SetDummyMinerSecret(key.GetBitcoinSecret(stratisminer.FullNode.Network));
                stratisminer.GenerateStratis(10);
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisminer));

                // push the wallet back
                stratisminer.FullNode.Services.ServiceProvider.GetService<IWalletSyncManager>().SyncFrom(5);

                stratisminer.GenerateStratis(5);

                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisminer));
            }
        }

        [Fact]
        public void WalletCanRecoverOnStartup()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                // get a key from the wallet
                var mnemonic = stratisNodeSync.FullNode.WalletManager.CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic.Words.Length);
                var addr = stratisNodeSync.FullNode.WalletManager.GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var key = stratisNodeSync.FullNode.WalletManager.GetKeyForAddress("mywallet", "123456", addr).PrivateKey;

                stratisNodeSync.SetDummyMinerSecret(key.GetBitcoinSecret(stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(10);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));

                // set the tip of best chain some blocks in the apst
                stratisNodeSync.FullNode.Chain.SetTip(stratisNodeSync.FullNode.Chain.GetBlock(stratisNodeSync.FullNode.Chain.Height - 5));

                // stop the node it will persist the chain with the reset tip
                stratisNodeSync.FullNode.Stop();

                var newNodeInstance = builder.CloneStratisNode(stratisNodeSync);

                // load the node, this should hit the block store recover code
                newNodeInstance.Start();

                // check that store recovered to be the same as the best chain.
                Assert.Equal(newNodeInstance.FullNode.Chain.Tip.HashBlock, newNodeInstance.FullNode.WalletManager.WalletTipHash);
            }
        }

        public static TransactionBuildContext CreateContext(WalletAccountReference accountReference, string password,
            Script destinationScript, Money amount, FeeType feeType, int minConfirmations)
        {
            return new TransactionBuildContext(accountReference,
                new[] { new Recipient { Amount = amount, ScriptPubKey = destinationScript } }.ToList(), password)
            {
                MinConfirmations = minConfirmations,
                FeeType = feeType
            };
        }
    }
}
