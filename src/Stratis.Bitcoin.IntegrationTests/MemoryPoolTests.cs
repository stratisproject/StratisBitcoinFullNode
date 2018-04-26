using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class MemoryPoolTests
    {
        public MemoryPoolTests()
        {
            Block.BlockSignature = false;
            Transaction.TimeStamp = false;
        }

        public class DateTimeProviderSet : DateTimeProvider
        {
            public long time;
            public DateTime timeutc;

            public override long GetTime()
            {
                return this.time;
            }

            public override DateTime GetUtcNow()
            {
                return this.timeutc;
            }
        }

        [Fact]
        public void AddToMempool()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();

                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                Transaction tx = new Transaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.MinerSecret, false);

                stratisNodeSync.Broadcast(tx);

                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void AddToMempoolTrxSpendingTwoOutputFromSameTrx()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(105); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest1 = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);
                var dest2 = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                Transaction parentTx = new Transaction();
                parentTx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                parentTx.AddOutput(new TxOut("25", dest1.PubKey.Hash));
                parentTx.AddOutput(new TxOut("24", dest2.PubKey.Hash)); // 1 btc fee
                parentTx.Sign(stratisNodeSync.MinerSecret, false);
                stratisNodeSync.Broadcast(parentTx);
                // wiat for the trx to enter the pool
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
                // mine the transactions in the mempool
                stratisNodeSync.GenerateStratis(1, stratisNodeSync.FullNode.MempoolManager().InfoAllAsync().Result.Select(s => s.Trx).ToList());
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

                //create a new trx spending both outputs
                Transaction tx = new Transaction();
                tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest1.PubKey)));
                tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest2.PubKey)));
                tx.AddOutput(new TxOut("48", new Key().PubKey.Hash)); // 1 btc fee
                var signed = new TransactionBuilder().AddKeys(dest1, dest2).AddCoins(parentTx.Outputs.AsCoins()).SignTransaction(tx);

                stratisNodeSync.Broadcast(signed);
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
            }
        }

        [Fact]
        public void MempoolReceiveFromManyNodes()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(201); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                var trxs = new List<Transaction>();
                foreach (var index in Enumerable.Range(1, 100))
                {
                    var block = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
                    var prevTrx = block.Transactions.First();
                    var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                    Transaction tx = new Transaction();
                    tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                    tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                    tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                    tx.Sign(stratisNodeSync.MinerSecret, false);
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

        [Fact]
        public void TxMempoolBlockDoublespend()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();
                stratisNodeSync.FullNode.Settings.RequireStandard = true; // make sure to test standard tx

                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(100); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                // Make sure skipping validation of transctions that were
                // validated going into the memory pool does not allow
                // double-spends in blocks to pass validation when they should not.

                var scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey);
                var genBlock = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;

                // Create a double-spend of mature coinbase txn:
                List<Transaction> spends = new List<Transaction>(2);
                foreach (var index in Enumerable.Range(1, 2))
                {
                    var trx = new Transaction();
                    trx.AddInput(new TxIn(new OutPoint(genBlock.Transactions[0].GetHash(), 0), scriptPubKey));
                    trx.AddOutput(Money.Cents(11), new Key().PubKey.Hash);
                    // Sign:
                    trx.Sign(stratisNodeSync.MinerSecret, false);
                    spends.Add(trx);
                }

                // Test 1: block with both of those transactions should be rejected.
                var block = stratisNodeSync.GenerateStratis(1, spends).Single();
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());

                // Test 2: ... and should be rejected if spend1 is in the memory pool
                Assert.True(stratisNodeSync.AddToStratisMempool(spends[0]));
                block = stratisNodeSync.GenerateStratis(1, spends).Single();
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());
                stratisNodeSync.FullNode.MempoolManager().Clear().Wait();

                // Test 3: ... and should be rejected if spend2 is in the memory pool
                Assert.True(stratisNodeSync.AddToStratisMempool(spends[1]));
                block = stratisNodeSync.GenerateStratis(1, spends).Single();
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());
                stratisNodeSync.FullNode.MempoolManager().Clear().Wait();

                // Final sanity test: first spend in mempool, second in block, that's OK:
                List<Transaction> oneSpend = new List<Transaction>();
                oneSpend.Add(spends[0]);
                Assert.True(stratisNodeSync.AddToStratisMempool(spends[1]));
                block = stratisNodeSync.GenerateStratis(1, oneSpend).Single();
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock == block.GetHash());

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

            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNode = builder.CreateStratisPowNode();
                builder.StartAll();

                stratisNode.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode.FullNode.Network));

                // 50 orphan transactions:
                for (ulong i = 0; i < 50; i++)
                {
                    Transaction tx = new Transaction();
                    tx.AddInput(new TxIn(new OutPoint(randHash(), 0), new Script(OpcodeType.OP_1)));
                    tx.AddOutput(new TxOut(new Money(1 * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));

                    stratisNode.FullNode.MempoolManager().Orphans.AddOrphanTx(i, tx).Wait();
                }

                Assert.Equal(50, stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count);

                // ... and 50 that depend on other orphans:
                for (ulong i = 0; i < 50; i++)
                {
                    var txPrev = stratisNode.FullNode.MempoolManager().Orphans.OrphansList().ElementAt(rand.Next(stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count));

                    Transaction tx = new Transaction();
                    tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), 0), new Script(OpcodeType.OP_1)));
                    tx.AddOutput(new TxOut(new Money((1 + i + 100) * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
                    stratisNode.FullNode.MempoolManager().Orphans.AddOrphanTx(i, tx).Wait();
                }

                Assert.Equal(100, stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count);

                // This really-big orphan should be ignored:
                for (ulong i = 0; i < 10; i++)
                {
                    var txPrev = stratisNode.FullNode.MempoolManager().Orphans.OrphansList().ElementAt(rand.Next(stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count));
                    Transaction tx = new Transaction();
                    tx.AddOutput(new TxOut(new Money(1 * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
                    foreach (var index in Enumerable.Range(0, 2777))
                        tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), index), new Script(OpcodeType.OP_1)));

                    Assert.False(stratisNode.FullNode.MempoolManager().Orphans.AddOrphanTx(i, tx).Result);
                }

                Assert.Equal(100, stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count);

                // Test EraseOrphansFor:
                for (ulong i = 0; i < 3; i++)
                {
                    var sizeBefore = stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count;
                    stratisNode.FullNode.MempoolManager().Orphans.EraseOrphansFor(i).Wait();
                    Assert.True(stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count < sizeBefore);
                }

                // Test LimitOrphanTxSize() function:
                stratisNode.FullNode.MempoolManager().Orphans.LimitOrphanTxSizeAsync(40).Wait();
                Assert.True(stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count <= 40);
                stratisNode.FullNode.MempoolManager().Orphans.LimitOrphanTxSizeAsync(10).Wait();
                Assert.True(stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Count <= 10);
                stratisNode.FullNode.MempoolManager().Orphans.LimitOrphanTxSizeAsync(0).Wait();
                Assert.True(!stratisNode.FullNode.MempoolManager().Orphans.OrphansList().Any());
            }
        }

        [Fact]
        public void MempoolAddNodeWithOrphans()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                builder.StartAll();
                stratisNodeSync.NotInIBD();

                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratis(101); // coinbase maturity = 100
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ConsensusLoop().Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.ChainBehaviorState.ConsensusTip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.HighestPersistedBlock().HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

                var block = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;
                var prevTrx = block.Transactions.First();
                var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                var key = new Key();
                Transaction tx = new Transaction();
                tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                tx.AddOutput(new TxOut("24", key.PubKey.Hash)); // 1 btc fee
                tx.Sign(stratisNodeSync.MinerSecret, false);

                Transaction txOrphan = new Transaction();
                txOrphan.AddInput(new TxIn(new OutPoint(tx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey)));
                txOrphan.AddOutput(new TxOut("10", new Key().PubKey.Hash));
                txOrphan.Sign(key.GetBitcoinSecret(stratisNodeSync.FullNode.Network), false);

                // broadcast the orphan
                stratisNodeSync.Broadcast(txOrphan);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().Orphans.OrphansList().Count == 1);
                // broadcast the parent
                stratisNodeSync.Broadcast(tx);
                TestHelper.WaitLoop(() => stratisNodeSync.FullNode.MempoolManager().Orphans.OrphansList().Count == 0);
                // wait for orphan to get in the pool
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 2);
            }
        }

        [Fact]
        public void MempoolSyncTransactions()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var stratisNodeSync = builder.CreateStratisPowNode();
                var stratisNode1 = builder.CreateStratisPowNode();
                var stratisNode2 = builder.CreateStratisPowNode();
                builder.StartAll();

                stratisNodeSync.NotInIBD();
                stratisNode1.NotInIBD();
                stratisNode2.NotInIBD();

                // generate blocks and wait for the downloader to pickup
                stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
                stratisNodeSync.GenerateStratisWithMiner(105); // coinbase maturity = 100
                // wait for block repo for block sync to work
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(stratisNodeSync));

                // sync both nodes
                stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode1, stratisNodeSync));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(stratisNode2, stratisNodeSync));

                // create some transactions and push them to the pool
                var trxs = new List<Transaction>();
                foreach (var index in Enumerable.Range(1, 5))
                {
                    var block = stratisNodeSync.FullNode.BlockStoreManager().BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
                    var prevTrx = block.Transactions.First();
                    var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

                    Transaction tx = new Transaction();
                    tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
                    tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
                    tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
                    tx.Sign(stratisNodeSync.MinerSecret, false);
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

                // reset the trickle timer on the full node that has the transactions in the pool
                foreach (var node in stratisNodeSync.FullNode.ConnectionManager.ConnectedPeers) node.Behavior<MempoolBehavior>().NextInvSend = 0;

                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetRawMempool().Length == 5);
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetRawMempool().Length == 5);

                // mine the transactions in the mempool
                stratisNodeSync.GenerateStratisWithMiner(1);
                TestHelper.WaitLoop(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

                // wait for block and mempool to change
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
                TestHelper.WaitLoop(() => stratisNode1.CreateRPCClient().GetRawMempool().Length == 0);
                TestHelper.WaitLoop(() => stratisNode2.CreateRPCClient().GetRawMempool().Length == 0);
            }
        }
    }

    public class TestMemPoolEntryHelper
    {
        // Default values
        private Money nFee = Money.Zero;

        private long nTime = 0;
        private double dPriority = 0.0;
        private int nHeight = 1;
        private bool spendsCoinbase = false;
        private long sigOpCost = 4;
        private LockPoints lp = new LockPoints();

        public TxMempoolEntry FromTx(Transaction tx, TxMempool pool = null)
        {
            Money inChainValue = (pool != null && pool.HasNoInputsOf(tx)) ? tx.TotalOut : 0;

            return new TxMempoolEntry(tx, this.nFee, this.nTime, this.dPriority, this.nHeight,
                inChainValue, this.spendsCoinbase, this.sigOpCost, this.lp, new PowConsensusOptions());
        }

        // Change the default value
        public TestMemPoolEntryHelper Fee(Money fee) { this.nFee = fee; return this; }

        public TestMemPoolEntryHelper Time(long time)
        {
            this.nTime = time; return this;
        }

        public TestMemPoolEntryHelper Priority(double priority)
        {
            this.dPriority = priority; return this;
        }

        public TestMemPoolEntryHelper Height(int height)
        {
            this.nHeight = height; return this;
        }

        public TestMemPoolEntryHelper SpendsCoinbase(bool flag)
        {
            this.spendsCoinbase = flag; return this;
        }

        public TestMemPoolEntryHelper SigOpsCost(long sigopsCost)
        {
            this.sigOpCost = sigopsCost; return this;
        }
    }
}
