using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Mempool
{
    public class MemoryPoolTests
    {
        private readonly Network network;

        public MemoryPoolTests()
        {
            this.network = KnownNetworks.RegTest;
        }

        [Fact]
        public void AddToMempool()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();

                builder.StartAll();

                TestHelper.MineBlocks(stratisNodeSync, 105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                Block block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                Transaction prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                Transaction tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);

                stratisNodeSync.Broadcast(tx);

                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void AddToMempoolTrxSpendingTwoOutputFromSameTrx()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();

                builder.StartAll();

                TestHelper.MineBlocks(stratisNodeSync, 105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                Block block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                Transaction prevTrx = block.Transactions.First();
                var dest1 = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);
                var dest2 = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                Transaction parentTx = stratisNodeSync.FullNode.Network.CreateTransaction();
                parentTx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                parentTx.AddOutput(new TxOut("25", dest1.PubKey.Hash));
                parentTx.AddOutput(new TxOut("24", dest2.PubKey.Hash)); // 1 btc fee
                parentTx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);

                stratisNodeSync.Broadcast(parentTx);
                // wiat for the trx to enter the pool
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
                // mine the transactions in the mempool
                TestHelper.GenerateBlockManually(stratisNodeSync, stratisNodeSync.FullNode.MempoolManager().InfoAllAsync().Result.Select(s => s.Trx).ToList());
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

                //create a new trx spending both outputs
                Transaction tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest1.PubKey)));
                tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest2.PubKey)));
                tx.AddOutput(new TxOut("48", new Key().PubKey.Hash)); // 1 btc fee
                Transaction signed = new TransactionBuilder(stratisNodeSync.FullNode.Network).AddKeys(dest1, dest2).AddCoins(parentTx.Outputs.AsCoins()).SignTransaction(tx);

                stratisNodeSync.Broadcast(signed);
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void MempoolReceiveFromManyNodes()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();
                builder.StartAll();

                TestHelper.MineBlocks(stratisNodeSync, 201); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                var trxs = new List<Transaction>();
                foreach (int index in Enumerable.Range(1, 100))
                {
                    Block block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
                    Transaction prevTrx = block.Transactions.First();
                    var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                    Transaction tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                    tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                    tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                    tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                    trxs.Add(tx);
                }
                var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
                Parallel.ForEach(trxs, options, transaction =>
                {
                    stratisNodeSync.Broadcast(transaction);
                });

                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 100);
            }
        }

        [Retry]
        public void TxMempoolBlockDoublespend()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();

                builder.StartAll();

                stratisNodeSync.FullNode.NodeService<MempoolSettings>().RequireStandard = true; // make sure to test standard tx

                TestHelper.MineBlocks(stratisNodeSync, 100); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                // Make sure skipping validation of transctions that were
                // validated going into the memory pool does not allow
                // double-spends in blocks to pass validation when they should not.

                Script scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey);
                Block genBlock = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;

                // Create a double-spend of mature coinbase txn:
                var spends = new List<Transaction>(2);
                foreach (int index in Enumerable.Range(1, 2))
                {
                    Transaction trx = stratisNodeSync.FullNode.Network.CreateTransaction();
                    trx.AddInput(new TxIn(new OutPoint(genBlock.Transactions[0].GetHash(), 0), scriptPubKey));
                    trx.AddOutput(Money.Cents(11), new Key().PubKey.Hash);
                    // Sign:
                    trx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                    spends.Add(trx);
                }

                // Test 1: block with both of those transactions should be rejected.
                var tipBeforeBlockCreation = stratisNodeSync.FullNode.Chain.Tip;
                Assert.Throws<ConsensusException>(() => { Block block = TestHelper.GenerateBlockManually(stratisNodeSync, spends); });
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock == tipBeforeBlockCreation.HashBlock);

                // Test 2: ... and should be rejected if spend1 is in the memory pool
                tipBeforeBlockCreation = stratisNodeSync.FullNode.Chain.Tip;
                Assert.True(stratisNodeSync.AddToStratisMempool(spends[0]));
                Assert.Throws<ConsensusException>(() => { Block block = TestHelper.GenerateBlockManually(stratisNodeSync, spends, 100_000); });
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock == tipBeforeBlockCreation.HashBlock);
                stratisNodeSync.FullNode.MempoolManager().Clear().Wait();

                // Test 3: ... and should be rejected if spend2 is in the memory pool
                tipBeforeBlockCreation = stratisNodeSync.FullNode.Chain.Tip;
                Assert.True(stratisNodeSync.AddToStratisMempool(spends[1]));
                Assert.Throws<ConsensusException>(() => { Block block = TestHelper.GenerateBlockManually(stratisNodeSync, spends, 100_000_000); });
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock == tipBeforeBlockCreation.HashBlock);
                stratisNodeSync.FullNode.MempoolManager().Clear().Wait();

                // Final sanity test: first spend in mempool, second in block, that's OK:
                var oneSpend = new List<Transaction>();
                oneSpend.Add(spends[0]);
                Assert.True(stratisNodeSync.AddToStratisMempool(spends[1]));
                var validBlock = TestHelper.GenerateBlockManually(stratisNodeSync, oneSpend);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock == validBlock.GetHash());

                // spends[1] should have been removed from the mempool when the
                // block with spends[0] is accepted:
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().MempoolSize().Result == 0);
            }
        }

        [Fact]
        public void TxMempoolMapOrphans()
        {
            var rand = new Random();
            var randByte = new byte[32];
            Func<uint256> randHash = () =>
            {
                rand.NextBytes(randByte);
                return new uint256(randByte);
            };

            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNode = builder.CreateStratisPowNode(this.network);
                builder.StartAll();

                stratisNode.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode.FullNode.Network));

                // 50 orphan transactions:
                for (ulong i = 0; i < 50; i++)
                {
                    Transaction tx = stratisNode.FullNode.Network.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(randHash(), 0), new Script(OpcodeType.OP_1)));
                    tx.AddOutput(new TxOut(new Money(1 * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));

                    stratisNode.FullNode.NodeService<MempoolOrphans>().AddOrphanTx(i, tx);
                }

                Assert.Equal(50, stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count);

                // ... and 50 that depend on other orphans:
                for (ulong i = 0; i < 50; i++)
                {
                    MempoolOrphans.OrphanTx txPrev = stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().ElementAt(rand.Next(stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count));

                    Transaction tx = stratisNode.FullNode.Network.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), 0), new Script(OpcodeType.OP_1)));
                    tx.AddOutput(new TxOut(new Money((1 + i + 100) * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
                    stratisNode.FullNode.NodeService<MempoolOrphans>().AddOrphanTx(i, tx);
                }

                Assert.Equal(100, stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count);

                // This really-big orphan should be ignored:
                for (ulong i = 0; i < 10; i++)
                {
                    MempoolOrphans.OrphanTx txPrev = stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().ElementAt(rand.Next(stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count));
                    Transaction tx = stratisNode.FullNode.Network.CreateTransaction();
                    tx.AddOutput(new TxOut(new Money(1 * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
                    foreach (int index in Enumerable.Range(0, 2777))
                        tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), index), new Script(OpcodeType.OP_1)));

                    Assert.False(stratisNode.FullNode.NodeService<MempoolOrphans>().AddOrphanTx(i, tx));
                }

                Assert.Equal(100, stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count);

                // Test EraseOrphansFor:
                for (ulong i = 0; i < 3; i++)
                {
                    int sizeBefore = stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count;
                    stratisNode.FullNode.NodeService<MempoolOrphans>().EraseOrphansFor(i);
                    Assert.True(stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count < sizeBefore);
                }

                // Test LimitOrphanTxSize() function:
                stratisNode.FullNode.NodeService<MempoolOrphans>().LimitOrphanTxSize(40);
                Assert.True(stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count <= 40);
                stratisNode.FullNode.NodeService<MempoolOrphans>().LimitOrphanTxSize(10);
                Assert.True(stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Count <= 10);
                stratisNode.FullNode.NodeService<MempoolOrphans>().LimitOrphanTxSize(0);
                Assert.True(!stratisNode.FullNode.NodeService<MempoolOrphans>().OrphansList().Any());
            }
        }

        [Fact]
        public void MempoolAddNodeWithOrphans()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();

                builder.StartAll();

                TestHelper.MineBlocks(stratisNodeSync, 101); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusManager().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.GetBlockStoreTip().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                Block block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;
                Transaction prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                var key = new Key();
                Transaction tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", key.PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);

                Transaction txOrphan = stratisNodeSync.FullNode.Network.CreateTransaction();
                txOrphan.AddInput(new TxIn(new OutPoint(tx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey)));
                txOrphan.AddOutput(new TxOut("10", new Key().PubKey.Hash));
                txOrphan.Sign(stratisNodeSync.FullNode.Network, key.GetBitcoinSecret(stratisNodeSync.FullNode.Network), false);

                // broadcast the orphan
                stratisNodeSync.Broadcast(txOrphan);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.NodeService<MempoolOrphans>().OrphansList().Count == 1);
                // broadcast the parent
                stratisNodeSync.Broadcast(tx);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.NodeService<MempoolOrphans>().OrphansList().Count == 0);
                // wait for orphan to get in the pool
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 2);
            }
        }

        [Fact]
        public void MempoolSyncTransactions()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode stratisNodeSync = builder.CreateStratisPowNode(this.network).NotInIBD().WithWallet();
                CoreNode stratisNode1 = builder.CreateStratisPowNode(this.network).NotInIBD();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(this.network).NotInIBD();

                builder.StartAll();

                // generate blocks and wait for the downloader to pickup
                TestHelper.MineBlocks(stratisNodeSync, 105); // coinbase maturity = 100

                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode1, stratisNodeSync));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode2, stratisNodeSync));

                // create some transactions and push them to the pool
                var trxs = new List<Transaction>();
                foreach (int index in Enumerable.Range(1, 5))
                {
                    Block block = stratisNodeSync.FullNode.BlockStore().GetBlockAsync(stratisNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
                    Transaction prevTrx = block.Transactions.First();
                    var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                    Transaction tx = stratisNodeSync.FullNode.Network.CreateTransaction();
                    tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                    tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                    tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                    tx.Sign(stratisNodeSync.FullNode.Network, stratisNodeSync.MinerSecret, false);
                    trxs.Add(tx);
                }
                var options = new ParallelOptions { MaxDegreeOfParallelism = 5 };
                Parallel.ForEach(trxs, options, transaction =>
                {
                    stratisNodeSync.Broadcast(transaction);
                });

                // wait for all nodes to have all trx
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 5);

                // the full node should be connected to both nodes
                Assert.True(stratisNodeSync.FullNode.ConnectionManager.ConnectedPeers.Count() >= 2);

                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetRawMempool().Length == 5);
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetRawMempool().Length == 5);

                // mine the transactions in the mempool
                TestHelper.MineBlocks(stratisNodeSync, 1);
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

                // wait for block and mempool to change
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetRawMempool().Length == 0);
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetRawMempool().Length == 0);
            }
        }
    }
}
