using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public class ProofOfWorkSpendingSpec : BddSpecification
    {
        private NodeBuilder nodeBuilder;
        private CoreNode stratisNodeSync;
        private CoreNode stratisNode1;
        private CoreNode stratisNode2;

        public ProofOfWorkSpendingSpec()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        [Fact]
        public void Dispose()
        {
            this.nodeBuilder.Dispose();
        }

        [Fact]
        public void Attempt_to_spend_coin_earned_through_proof_of_work_as_soon_as_it_is_mined_will_fail()
        {
            Given(a_bitcoin_node_on_regtest);
            And(proof_of_work_blocks_have_just_been_mined);
            And(maturity_has_not_been_reached);
            When(i_try_to_spend_the_coins);
            Then(then_transaction_should_be_rejected_from_the_mempool);


            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisSender = builder.CreateStratisPowNode();
                var stratisReceiver = builder.CreateStratisPowNode();
                var stratisReorg = builder.CreateStratisPowNode();

                builder.StartAll();
                stratisSender.NotInIBD();
                stratisReceiver.NotInIBD();
                stratisReorg.NotInIBD();

                // get a key from the wallet
                var mnemonic1 = stratisSender.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                var mnemonic2 = stratisReceiver.FullNode.WalletManager().CreateWallet("123456", "mywallet");
                Assert.Equal(12, mnemonic1.Words.Length);
                Assert.Equal(12, mnemonic2.Words.Length);
                var addr = stratisSender.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var wallet = stratisSender.FullNode.WalletManager().GetWalletByName("mywallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("123456", addr).PrivateKey;

                stratisSender.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));
                stratisReorg.SetDummyMinerSecret(new BitcoinSecret(key, stratisSender.FullNode.Network));

                var maturity = (int)stratisSender.FullNode.Network.Consensus.Option<PowConsensusOptions>().CoinbaseMaturity;
                stratisSender.GenerateStratisWithMiner(maturity + 15);

                var currentBestHeight = maturity + 15;

                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));

                // the mining should add coins to the wallet
                var total = stratisSender.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
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
                var sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction1 = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 100, FeeType.Medium, 101));

                // broadcast to the other node
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction1.ToHex()));

                // wait for the trx to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction1.GetHash(), false));
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());

                var receivetotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 100, receivetotal);
                Assert.Null(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

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
                TestHelper.WaitLoop(() => transaction1MinedHeight == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // Build Transaction 2
                // ====================
                // remove the reorg node
                stratisReceiver.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                stratisSender.CreateRPCClient().RemoveNode(stratisReorg.Endpoint);
                var forkblock = stratisReceiver.FullNode.Chain.Tip;

                // send more coins to the wallet
                sendto = stratisReceiver.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("mywallet", "account 0"));
                var transaction2 = stratisSender.FullNode.WalletTransactionHandler().BuildTransaction(CreateContext(new WalletAccountReference("mywallet", "account 0"), "123456", sendto.ScriptPubKey, Money.COIN * 10, FeeType.Medium, 101));
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));
                // wait for the trx to arrive
                TestHelper.WaitLoop(() => stratisReceiver.CreateRPCClient().GetRawMempool().Length > 0);
                Assert.NotNull(stratisReceiver.CreateRPCClient().GetRawTransaction(transaction2.GetHash(), false));
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any());
                var newamount = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(Money.COIN * 110, newamount);
                Assert.Contains(stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet"), b => b.Transaction.BlockHeight == null);

                // mine more blocks so its included in the chain

                stratisSender.GenerateStratisWithMiner(1);
                var transaction2MinedHeight = currentBestHeight + 1;
                stratisSender.GenerateStratisWithMiner(1);
                currentBestHeight = currentBestHeight + 2;
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisSender));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisReceiver, stratisSender));
                Assert.Equal(currentBestHeight, stratisReceiver.FullNode.Chain.Tip.Height);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));

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
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().WalletTipHash == stratisReorg.CreateRPCClient().GetBestBlockHash());
                // check the wallet amount was rolled back
                var newtotal = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(receivetotal, newtotal);
                TestHelper.WaitLoop(() => maturity + 16 == stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").First().Transaction.BlockHeight);

                // ReBuild Transaction 2
                // ====================
                // After the reorg transaction2 was returned back to mempool
                stratisSender.FullNode.NodeService<WalletController>().SendTransaction(new SendTransactionRequest(transaction2.ToHex()));

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
                var newsecondamount = stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Sum(s => s.Transaction.Amount);
                Assert.Equal(newamount, newsecondamount);
                TestHelper.WaitLoop(() => stratisReceiver.FullNode.WalletManager().GetSpendableTransactionsInWallet("mywallet").Any(b => b.Transaction.BlockHeight == transaction2MinedHeight));
            }
            //    // three_nodes_not_in_initial_block_download
            //    this.stratisNodeSync = this.nodeBuilder.CreateStratisPowNode();
            //    this.stratisNode1 = this.nodeBuilder.CreateStratisPowNode();
            //    this.stratisNode2 = this.nodeBuilder.CreateStratisPowNode();

            //    this.nodeBuilder.StartAll();

            //    this.stratisNodeSync.NotInIBD();
            //    this.stratisNode1.NotInIBD();
            //    this.stratisNode2.NotInIBD();

            //    // generate blocks and wait for the downloader to pickup
            //    this.stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), this.stratisNodeSync.FullNode.Network));
            //    this.stratisNodeSync.GenerateStratisWithMiner(10); // coinbase maturity = 10

            //    // wait for block repo for block sync to work
            //        TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
            //        TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
            //        TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

            //        // sync both nodes
            //        stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
            //        stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
            //        TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
            //        TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());

            //        // set node2 to use inv (not headers)
            //        stratisNode2.FullNode.ConnectionManager.ConnectedPeers.First().Behavior<BlockStoreBehavior>().PreferHeaders = false;

            //        // generate two new blocks
            //        stratisNodeSync.GenerateStratisWithMiner(2);
            //        // wait for block repo for block sync to work
            //        TestHelper.WaitLoop(() => stratisNodeSync.FullNode.Chain.Tip.HashBlock == stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock);
            //        TestHelper.WaitLoop(() => stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null);

            //        // wait for the other nodes to pick up the newly generated blocks
            //        TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
            //        TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
            //}
        }

        private void a_bitcoin_node_on_regtest()
        {
            throw new System.NotImplementedException();
        }

        private void proof_of_work_blocks_have_just_been_mined()
        {
            throw new System.NotImplementedException();
        }

        private void maturity_has_not_been_reached()
        {
            throw new System.NotImplementedException();
        }

        private void i_try_to_spend_the_coins()
        {
            throw new System.NotImplementedException();
        }

        private void then_transaction_should_be_rejected_from_the_mempool()
        {
            throw new System.NotImplementedException();
        }
    }
