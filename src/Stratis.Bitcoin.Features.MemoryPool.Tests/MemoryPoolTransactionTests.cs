using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    public class MemoryPoolTransactionTests
    {
        [Fact]
        public void MempoolRemoveTest()
        {
            var entry = new TestMemPoolEntryHelper();

            // Parent transaction with three children,
            // and three grand-children:
            var txParent = new Transaction();

            txParent.AddInput(new TxIn());
            txParent.Inputs[0].ScriptSig = new Script(OpcodeType.OP_11);

            for (int i = 0; i < 3; i++)
            {
                txParent.AddOutput(new TxOut(new Money(33000L), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            }

            var txChild = new Transaction[3];
            for (int i = 0; i < 3; i++)
            {
                txChild[i] = new Transaction();
                txChild[i].AddInput(new TxIn(new OutPoint(txParent, i), new Script(OpcodeType.OP_11)));
                txChild[i].AddOutput(new TxOut(new Money(11000L), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            }

            var txGrandChild = new Transaction[3];
            for (int i = 0; i < 3; i++)
            {
                txGrandChild[i] = new Transaction();
                txGrandChild[i].AddInput(new TxIn(new OutPoint(txChild[i], 0), new Script(OpcodeType.OP_11)));
                txGrandChild[i].AddOutput(new TxOut(new Money(11000L), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            }

            NodeSettings settings = NodeSettings.Default(KnownNetworks.TestNet);
            var testPool = new TxMempool(DateTimeProvider.Default, new BlockPolicyEstimator(new MempoolSettings(settings), settings.LoggerFactory, settings), settings.LoggerFactory, settings);

            // Nothing in pool, remove should do nothing:
            long poolSize = testPool.Size;
            testPool.RemoveRecursive(txParent);
            Assert.Equal(testPool.Size, poolSize);

            // Just the parent:
            testPool.AddUnchecked(txParent.GetHash(), entry.FromTx(txParent));
            poolSize = testPool.Size;
            testPool.RemoveRecursive(txParent);
            Assert.Equal(testPool.Size, poolSize - 1);

            // Parent, children, grandchildren:
            testPool.AddUnchecked(txParent.GetHash(), entry.FromTx(txParent));
            for (int i = 0; i < 3; i++)
            {
                testPool.AddUnchecked(txChild[i].GetHash(), entry.FromTx(txChild[i]));
                testPool.AddUnchecked(txGrandChild[i].GetHash(), entry.FromTx(txGrandChild[i]));
            }
            // Remove Child[0], GrandChild[0] should be removed:
            poolSize = testPool.Size;
            testPool.RemoveRecursive(txChild[0]);
            Assert.Equal(testPool.Size, poolSize - 2);
            // ... make sure grandchild and child are gone:
            poolSize = testPool.Size;
            testPool.RemoveRecursive(txGrandChild[0]);
            Assert.Equal(testPool.Size, poolSize);
            poolSize = testPool.Size;
            testPool.RemoveRecursive(txChild[0]);
            Assert.Equal(testPool.Size, poolSize);
            // Remove parent, all children/grandchildren should go:
            poolSize = testPool.Size;
            testPool.RemoveRecursive(txParent);
            Assert.Equal(testPool.Size, poolSize - 5);
            Assert.Equal(0, testPool.Size);

            // Add children and grandchildren, but NOT the parent (simulate the parent being in a block)
            for (int i = 0; i < 3; i++)
            {
                testPool.AddUnchecked(txChild[i].GetHash(), entry.FromTx(txChild[i]));
                testPool.AddUnchecked(txGrandChild[i].GetHash(), entry.FromTx(txGrandChild[i]));
            }
            // Now remove the parent, as might happen if a block-re-org occurs but the parent cannot be
            // put into the mempool (maybe because it is non-standard):
            poolSize = testPool.Size;
            testPool.RemoveRecursive(txParent);
            Assert.Equal(poolSize - 6, testPool.Size);
            Assert.Equal(0, testPool.Size);
        }

        private void CheckSort(TxMempool pool, List<TxMempoolEntry> sortedSource, List<string> sortedOrder)
        {
            Assert.Equal(pool.Size, sortedOrder.Count());
            int count = 0;
            using (List<TxMempoolEntry>.Enumerator it = sortedSource.GetEnumerator())
            {
                for (; it.MoveNext(); ++count)
                {
                    Assert.Equal(it.Current.TransactionHash.ToString(), sortedOrder[count]);
                }
            }
        }

        [Fact]
        public void MempoolIndexingTest()
        {
            NodeSettings settings = NodeSettings.Default(KnownNetworks.TestNet);
            var pool = new TxMempool(DateTimeProvider.Default, new BlockPolicyEstimator(new MempoolSettings(settings), settings.LoggerFactory, settings), settings.LoggerFactory, settings);
            var entry = new TestMemPoolEntryHelper();

            /* 3rd highest fee */
            var tx1 = new Transaction();
            tx1.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx1.GetHash(), entry.Fee(new Money(10000L)).Priority(10.0).FromTx(tx1));

            /* highest fee */
            var tx2 = new Transaction();
            tx2.AddOutput(new TxOut(new Money(2 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx2.GetHash(), entry.Fee(new Money(20000L)).Priority(9.0).FromTx(tx2));

            /* lowest fee */
            var tx3 = new Transaction();
            tx3.AddOutput(new TxOut(new Money(5 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx3.GetHash(), entry.Fee(new Money(0L)).Priority(100.0).FromTx(tx3));

            /* 2nd highest fee */
            var tx4 = new Transaction();
            tx4.AddOutput(new TxOut(new Money(6 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx4.GetHash(), entry.Fee(new Money(15000L)).Priority(1.0).FromTx(tx4));

            /* equal fee rate to tx1, but newer */
            var tx5 = new Transaction();
            tx5.AddOutput(new TxOut(new Money(11 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx5.GetHash(), entry.Fee(new Money(10000L)).Priority(10.0).Time(1).FromTx(tx5));

            // assert size
            Assert.Equal(5, pool.Size);

            var sortedOrder = new List<string>(5);
            sortedOrder.Insert(0, tx3.GetHash().ToString()); // 0
            sortedOrder.Insert(1, tx5.GetHash().ToString()); // 10000
            sortedOrder.Insert(2, tx1.GetHash().ToString()); // 10000
            sortedOrder.Insert(3, tx4.GetHash().ToString()); // 15000
            sortedOrder.Insert(4, tx2.GetHash().ToString()); // 20000
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

            /* low fee but with high fee child */
            /* tx6 -> tx7 -> tx8, tx9 -> tx10 */
            var tx6 = new Transaction();
            tx6.AddOutput(new TxOut(new Money(20 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx6.GetHash(), entry.Fee(new Money(0L)).FromTx(tx6));

            // assert size
            Assert.Equal(6, pool.Size);

            // Check that at this point, tx6 is sorted low
            sortedOrder.Insert(0, tx6.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

            var setAncestors = new TxMempool.SetEntries();
            setAncestors.Add(pool.MapTx.TryGet(tx6.GetHash()));
            var tx7 = new Transaction();
            tx7.AddInput(new TxIn(new OutPoint(tx6.GetHash(), 0), new Script(OpcodeType.OP_11)));
            tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            tx7.AddOutput(new TxOut(new Money(1 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));

            var setAncestorsCalculated = new TxMempool.SetEntries();
            string dummy;
            Assert.True(pool.CalculateMemPoolAncestors(entry.Fee(2000000L).FromTx(tx7), setAncestorsCalculated, 100, 1000000, 1000, 1000000, out dummy));
            Assert.True(setAncestorsCalculated.Equals(setAncestors));

            pool.AddUnchecked(tx7.GetHash(), entry.FromTx(tx7), setAncestors);
            Assert.Equal(7, pool.Size);

            // Now tx6 should be sorted higher (high fee child): tx7, tx6, tx2, ...
            sortedOrder.RemoveAt(0);
            sortedOrder.Add(tx6.GetHash().ToString());
            sortedOrder.Add(tx7.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

            /* low fee child of tx7 */
            var tx8 = new Transaction();
            tx8.AddInput(new TxIn(new OutPoint(tx7.GetHash(), 0), new Script(OpcodeType.OP_11)));
            tx8.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            setAncestors.Add(pool.MapTx.TryGet(tx7.GetHash()));
            pool.AddUnchecked(tx8.GetHash(), entry.Fee(0L).Time(2).FromTx(tx8), setAncestors);

            // Now tx8 should be sorted low, but tx6/tx both high
            sortedOrder.Insert(0, tx8.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

            /* low fee child of tx7 */
            var tx9 = new Transaction();
            tx9.AddInput(new TxIn(new OutPoint(tx7.GetHash(), 1), new Script(OpcodeType.OP_11)));
            tx9.AddOutput(new TxOut(new Money(1 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx9.GetHash(), entry.Fee(0L).Time(3).FromTx(tx9), setAncestors);

            // tx9 should be sorted low
            Assert.Equal(9, pool.Size);

            sortedOrder.Insert(0, tx9.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

            List<string> snapshotOrder = sortedOrder.ToList();

            setAncestors.Add(pool.MapTx.TryGet(tx8.GetHash()));
            setAncestors.Add(pool.MapTx.TryGet(tx9.GetHash()));
            /* tx10 depends on tx8 and tx9 and has a high fee*/
            var tx10 = new Transaction();
            tx10.AddInput(new TxIn(new OutPoint(tx8.GetHash(), 0), new Script(OpcodeType.OP_11)));
            tx10.AddInput(new TxIn(new OutPoint(tx9.GetHash(), 0), new Script(OpcodeType.OP_11)));
            tx10.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));

            setAncestorsCalculated.Clear();
            Assert.True(pool.CalculateMemPoolAncestors(entry.Fee(200000L).Time(4).FromTx(tx10), setAncestorsCalculated, 100, 1000000, 1000, 1000000, out dummy));
            Assert.True(setAncestorsCalculated.Equals(setAncestors));

            pool.AddUnchecked(tx10.GetHash(), entry.FromTx(tx10), setAncestors);

            /**
             *  tx8 and tx9 should both now be sorted higher
             *  Final order after tx10 is added:
             *
             *  tx3 = 0 (1)
             *  tx5 = 10000 (1)
             *  tx1 = 10000 (1)
             *  tx4 = 15000 (1)
             *  tx2 = 20000 (1)
             *  tx9 = 200k (2 txs)
             *  tx8 = 200k (2 txs)
             *  tx10 = 200k (1 tx)
             *  tx6 = 2.2M (5 txs)
             *  tx7 = 2.2M (4 txs)
             */
            sortedOrder.RemoveRange(0, 2); // take out tx9, tx8 from the beginning
            sortedOrder.Insert(5, tx9.GetHash().ToString());
            sortedOrder.Insert(6, tx8.GetHash().ToString());
            sortedOrder.Insert(7, tx10.GetHash().ToString()); // tx10 is just before tx6
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

            // there should be 10 transactions in the mempool
            Assert.Equal(10, pool.Size);

            // Now try removing tx10 and verify the sort order returns to normal
            pool.RemoveRecursive(pool.MapTx.TryGet(tx10.GetHash()).Transaction);
            this.CheckSort(pool, pool.MapTx.DescendantScore.ToList(), snapshotOrder);

            pool.RemoveRecursive(pool.MapTx.TryGet(tx9.GetHash()).Transaction);
            pool.RemoveRecursive(pool.MapTx.TryGet(tx8.GetHash()).Transaction);
            /* Now check the sort on the mining score index.
             * Final order should be:
             *
             * tx7 (2M)
             * tx2 (20k)
             * tx4 (15000)
             * tx1/tx5 (10000)
             * tx3/6 (0)
             * (Ties resolved by hash)
             */
            sortedOrder.Clear();
            sortedOrder.Add(tx7.GetHash().ToString());
            sortedOrder.Add(tx2.GetHash().ToString());
            sortedOrder.Add(tx4.GetHash().ToString());
            if (tx1.GetHash() < tx5.GetHash())
            {
                sortedOrder.Add(tx5.GetHash().ToString());
                sortedOrder.Add(tx1.GetHash().ToString());
            }
            else
            {
                sortedOrder.Add(tx1.GetHash().ToString());
                sortedOrder.Add(tx5.GetHash().ToString());
            }
            if (tx3.GetHash() < tx6.GetHash())
            {
                sortedOrder.Add(tx6.GetHash().ToString());
                sortedOrder.Add(tx3.GetHash().ToString());
            }
            else
            {
                sortedOrder.Add(tx3.GetHash().ToString());
                sortedOrder.Add(tx6.GetHash().ToString());
            }
            this.CheckSort(pool, pool.MapTx.MiningScore.ToList(), sortedOrder);
        }

        [Fact]
        public void MempoolAncestorIndexingTest()
        {
            NodeSettings settings = NodeSettings.Default(KnownNetworks.TestNet);
            var pool = new TxMempool(DateTimeProvider.Default, new BlockPolicyEstimator(new MempoolSettings(settings), settings.LoggerFactory, settings), settings.LoggerFactory, settings);
            var entry = new TestMemPoolEntryHelper();

            /* 3rd highest fee */
            var tx1 = new Transaction();
            tx1.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx1.GetHash(), entry.Fee(new Money(10000L)).Priority(10.0).FromTx(tx1));

            /* highest fee */
            var tx2 = new Transaction();
            tx2.AddOutput(new TxOut(new Money(2 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx2.GetHash(), entry.Fee(new Money(20000L)).Priority(9.0).FromTx(tx2));
            int tx2Size = tx2.GetVirtualSize();

            /* lowest fee */
            var tx3 = new Transaction();
            tx3.AddOutput(new TxOut(new Money(5 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx3.GetHash(), entry.Fee(new Money(0L)).Priority(100.0).FromTx(tx3));

            /* 2nd highest fee */
            var tx4 = new Transaction();
            tx4.AddOutput(new TxOut(new Money(6 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx4.GetHash(), entry.Fee(new Money(15000L)).Priority(1.0).FromTx(tx4));

            /* equal fee rate to tx1, but newer */
            var tx5 = new Transaction();
            tx5.AddOutput(new TxOut(new Money(11 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx5.GetHash(), entry.Fee(new Money(10000L)).Priority(1.0).FromTx(tx5));

            // assert size
            Assert.Equal(5, pool.Size);

            var sortedOrder = new List<string>(5);
            sortedOrder.Insert(0, tx2.GetHash().ToString()); // 20000
            sortedOrder.Insert(1, tx4.GetHash().ToString()); // 15000

            // tx1 and tx5 are both 10000
            // Ties are broken by hash, not timestamp, so determine which
            // hash comes first.
            if (tx1.GetHash() < tx5.GetHash())
            {
                sortedOrder.Insert(2, tx1.GetHash().ToString());
                sortedOrder.Insert(3, tx5.GetHash().ToString());
            }
            else
            {
                sortedOrder.Insert(2, tx5.GetHash().ToString());
                sortedOrder.Insert(3, tx1.GetHash().ToString());
            }
            sortedOrder.Insert(4, tx3.GetHash().ToString()); // 0
            this.CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);

            /* low fee parent with high fee child */
            /* tx6 (0) -> tx7 (high) */
            var tx6 = new Transaction();
            tx6.AddOutput(new TxOut(new Money(20 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx6.GetHash(), entry.Fee(new Money(0L)).FromTx(tx6));
            int tx6Size = tx6.GetVirtualSize();
            Assert.Equal(6, pool.Size);
            // Ties are broken by hash
            if (tx3.GetHash() < tx6.GetHash())
                sortedOrder.Add(tx6.GetHash().ToString());
            else
                sortedOrder.Insert(sortedOrder.Count - 1, tx6.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);

            var tx7 = new Transaction();
            tx7.AddInput(new TxIn(new OutPoint(tx6.GetHash(), 0), new Script(OpcodeType.OP_11)));
            tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
            int tx7Size = tx7.GetVirtualSize();
            Money fee = (20000 / tx2Size) * (tx7Size + tx6Size) - 1;
            pool.AddUnchecked(tx7.GetHash(), entry.Fee(fee).FromTx(tx7));
            Assert.Equal(7, pool.Size);
            sortedOrder.Insert(1, tx7.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);

            /* after tx6 is mined, tx7 should move up in the sort */
            var vtx = new List<Transaction>(new[] { tx6 });
            pool.RemoveForBlock(vtx, 1);

            sortedOrder.RemoveAt(1);
            // Ties are broken by hash
            if (tx3.GetHash() < tx6.GetHash())
                sortedOrder.Remove(sortedOrder.Last());
            else
                sortedOrder.RemoveAt(sortedOrder.Count - 2);
            sortedOrder.Insert(0, tx7.GetHash().ToString());
            this.CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);
        }

        [Fact]
        public void MempoolSizeLimitTest()
        {
            NodeSettings settings = NodeSettings.Default(KnownNetworks.TestNet);
            var dateTimeSet = new DateTimeProviderSet();
            var pool = new TxMempool(dateTimeSet, new BlockPolicyEstimator(new MempoolSettings(settings), settings.LoggerFactory, settings), settings.LoggerFactory, settings);
            var entry = new TestMemPoolEntryHelper();
            entry.Priority(10.0);

            var tx1 = new Transaction();
            tx1.AddInput(new TxIn(new Script(OpcodeType.OP_1)));
            tx1.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_1, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx1.GetHash(), entry.Fee(10000L).FromTx(tx1, pool));

            var tx2 = new Transaction();
            tx2.AddInput(new TxIn(new Script(OpcodeType.OP_2)));
            tx2.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_2, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx2.GetHash(), entry.Fee(5000L).FromTx(tx2, pool));

            pool.TrimToSize(pool.DynamicMemoryUsage()); // should do nothing
            Assert.True(pool.Exists(tx1.GetHash()));
            Assert.True(pool.Exists(tx2.GetHash()));

            pool.TrimToSize(pool.DynamicMemoryUsage() * 3 / 4); // should remove the lower-feerate transaction
            Assert.True(pool.Exists(tx1.GetHash()));
            Assert.True(!pool.Exists(tx2.GetHash()));

            pool.AddUnchecked(tx2.GetHash(), entry.FromTx(tx2, pool));
            var tx3 = new Transaction();
            tx3.AddInput(new TxIn(new OutPoint(tx2.GetHash(), 0), new Script(OpcodeType.OP_2)));
            tx3.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_3, OpcodeType.OP_EQUAL)));
            pool.AddUnchecked(tx3.GetHash(), entry.Fee(20000L).FromTx(tx3, pool));

            pool.TrimToSize(pool.DynamicMemoryUsage() * 3 / 4); // tx3 should pay for tx2 (CPFP)
            Assert.True(!pool.Exists(tx1.GetHash()));
            Assert.True(pool.Exists(tx2.GetHash()));
            Assert.True(pool.Exists(tx3.GetHash()));

            pool.TrimToSize(tx1.GetVirtualSize()); // mempool is limited to tx1's size in memory usage, so nothing fits
            Assert.True(!pool.Exists(tx1.GetHash()));
            Assert.True(!pool.Exists(tx2.GetHash()));
            Assert.True(!pool.Exists(tx3.GetHash()));

            var maxFeeRateRemoved = new FeeRate(25000, tx3.GetVirtualSize() + tx2.GetVirtualSize());
            Assert.Equal(pool.GetMinFee(1).FeePerK, maxFeeRateRemoved.FeePerK + 1000);

            var tx4 = new Transaction();
            tx4.AddInput(new TxIn(new Script(OpcodeType.OP_4)));
            tx4.AddInput(new TxIn(new Script(OpcodeType.OP_4)));
            tx4.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_4, OpcodeType.OP_EQUAL)));
            tx4.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_4, OpcodeType.OP_EQUAL)));

            var tx5 = new Transaction();
            tx5.AddInput(new TxIn(new OutPoint(tx4.GetHash(), 0), new Script(OpcodeType.OP_4)));
            tx5.AddInput(new TxIn(new Script(OpcodeType.OP_5)));
            tx5.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_5, OpcodeType.OP_EQUAL)));
            tx5.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_5, OpcodeType.OP_EQUAL)));

            var tx6 = new Transaction();
            tx6.AddInput(new TxIn(new OutPoint(tx4.GetHash(), 0), new Script(OpcodeType.OP_4)));
            tx6.AddInput(new TxIn(new Script(OpcodeType.OP_6)));
            tx6.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_6, OpcodeType.OP_EQUAL)));
            tx6.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_6, OpcodeType.OP_EQUAL)));

            var tx7 = new Transaction();
            tx7.AddInput(new TxIn(new OutPoint(tx5.GetHash(), 0), new Script(OpcodeType.OP_5)));
            tx7.AddInput(new TxIn(new OutPoint(tx6.GetHash(), 0), new Script(OpcodeType.OP_6)));
            tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_7, OpcodeType.OP_EQUAL)));
            tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_7, OpcodeType.OP_EQUAL)));

            pool.AddUnchecked(tx4.GetHash(), entry.Fee(7000L).FromTx(tx4, pool));
            pool.AddUnchecked(tx5.GetHash(), entry.Fee(1000L).FromTx(tx5, pool));
            pool.AddUnchecked(tx6.GetHash(), entry.Fee(1100L).FromTx(tx6, pool));
            pool.AddUnchecked(tx7.GetHash(), entry.Fee(9000L).FromTx(tx7, pool));

            // we only require this remove, at max, 2 txn, because its not clear what we're really optimizing for aside from that
            pool.TrimToSize(pool.DynamicMemoryUsage() - 1);
            Assert.True(pool.Exists(tx4.GetHash()));
            Assert.True(pool.Exists(tx6.GetHash()));
            Assert.True(!pool.Exists(tx7.GetHash()));

            if (!pool.Exists(tx5.GetHash()))
                pool.AddUnchecked(tx5.GetHash(), entry.Fee(1000L).FromTx(tx5, pool));
            pool.AddUnchecked(tx7.GetHash(), entry.Fee(9000L).FromTx(tx7, pool));

            pool.TrimToSize(pool.DynamicMemoryUsage() / 2); // should maximize mempool size by only removing 5/7
            Assert.True(pool.Exists(tx4.GetHash()));
            Assert.True(!pool.Exists(tx5.GetHash()));
            Assert.True(pool.Exists(tx6.GetHash()));
            Assert.True(!pool.Exists(tx7.GetHash()));

            pool.AddUnchecked(tx5.GetHash(), entry.Fee(1000L).FromTx(tx5, pool));
            pool.AddUnchecked(tx7.GetHash(), entry.Fee(9000L).FromTx(tx7, pool));

            var vtx = new List<Transaction>();
            dateTimeSet.time = 42 + TxMempool.RollingFeeHalflife;
            Assert.Equal(pool.GetMinFee(1).FeePerK.Satoshi, maxFeeRateRemoved.FeePerK.Satoshi + 1000);
            // ... we should keep the same min fee until we get a block
            pool.RemoveForBlock(vtx, 1);
            dateTimeSet.time = 42 + 2 * +TxMempool.RollingFeeHalflife;
            Assert.Equal(pool.GetMinFee(1).FeePerK.Satoshi, (maxFeeRateRemoved.FeePerK.Satoshi + 1000) / 2);
            // ... then feerate should drop 1/2 each halflife

            dateTimeSet.time = 42 + 2 * TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife / 2;
            Assert.Equal(pool.GetMinFee(pool.DynamicMemoryUsage() * 5 / 2).FeePerK.Satoshi, (maxFeeRateRemoved.FeePerK.Satoshi + 1000) / 4);
            // ... with a 1/2 halflife when mempool is < 1/2 its target size

            dateTimeSet.time = 42 + 2 * TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife / 2 + TxMempool.RollingFeeHalflife / 4;
            Assert.Equal(pool.GetMinFee(pool.DynamicMemoryUsage() * 9 / 2).FeePerK.Satoshi, (maxFeeRateRemoved.FeePerK.Satoshi + 1000) / 8);
            // ... with a 1/4 halflife when mempool is < 1/4 its target size

            dateTimeSet.time = 42 + 7 * TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife / 2 + TxMempool.RollingFeeHalflife / 4;
            Assert.Equal(1000, pool.GetMinFee(1).FeePerK.Satoshi);
            // ... but feerate should never drop below 1000

            dateTimeSet.time = 42 + 8 * TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife / 2 + TxMempool.RollingFeeHalflife / 4;
            Assert.Equal(0, pool.GetMinFee(1).FeePerK);
            // ... unless it has gone all the way to 0 (after getting past 1000/2)
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
        public void MempoolConcurrencyTest()
        {
            NodeSettings settings = NodeSettings.Default(KnownNetworks.TestNet);
            var pool = new TxMempool(DateTimeProvider.Default, new BlockPolicyEstimator(new MempoolSettings(settings), settings.LoggerFactory, settings), settings.LoggerFactory, settings);
            var scheduler = new SchedulerLock();
            var rand = new Random();

            int value = 10000;
            var txs = new List<Transaction>();
            for (int i = 0; i < 20; i++)
            {
                var tx = new Transaction();
                tx.AddInput(new TxIn(new Script(OpcodeType.OP_11)));
                tx.AddOutput(new TxOut(new Money(value++), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
                txs.Add(tx);
            }

            var tasks = new List<Task>();
            var options = new ParallelOptions { MaxDegreeOfParallelism = 10 };
            Parallel.ForEach(txs, options, transaction =>
            {
                var entry = new TxMempoolEntry(transaction, new Money(rand.Next(100)), 0, 0.0, 1, transaction.TotalOut, false, 4, new LockPoints(), new ConsensusOptions());
                tasks.Add(scheduler.WriteAsync(() => pool.AddUnchecked(transaction.GetHash(), entry)));
            });

            Task.WaitAll(tasks.ToArray());
            Assert.Equal(20, scheduler.ReadAsync(() => pool.Size).Result);
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
                inChainValue, this.spendsCoinbase, this.sigOpCost, this.lp, new ConsensusOptions());
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
