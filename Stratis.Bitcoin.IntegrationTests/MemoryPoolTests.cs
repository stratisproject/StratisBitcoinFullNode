using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class MemoryPoolTests
	{
		[Fact]
		public void MempoolRemoveTest()
	    {
			// Test CTxMemPool::remove functionality

			TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();

			// Parent transaction with three children,
			// and three grand-children:
			Transaction txParent = new Transaction();

		    txParent.AddInput(new TxIn());
		    txParent.Inputs[0].ScriptSig = new Script(OpcodeType.OP_11);

			for (int i = 0; i < 3; i++)
			{
				txParent.AddOutput(new TxOut(new Money(33000L), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			}

		    Transaction[] txChild = new Transaction[3];
			for (int i = 0; i < 3; i++)
			{
				txChild[i] = new Transaction();
				txChild[i].AddInput(new TxIn(new OutPoint(txParent, i), new Script(OpcodeType.OP_11)));
				txChild[i].AddOutput(new TxOut(new Money(11000L), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			}

			Transaction[] txGrandChild = new Transaction[3];
			for (int i = 0; i < 3; i++)
			{
				txGrandChild[i] = new Transaction();
				txGrandChild[i].AddInput(new TxIn(new OutPoint(txChild[i], 0), new Script(OpcodeType.OP_11)));
				txGrandChild[i].AddOutput(new TxOut(new Money(11000L), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			}

			TxMempool testPool = new TxMempool(new FeeRate(0), NodeSettings.Default());

			// Nothing in pool, remove should do nothing:
			var poolSize = testPool.Size;
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
			Assert.Equal(testPool.Size, 0);

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
			Assert.Equal(testPool.Size, poolSize - 6);
			Assert.Equal(testPool.Size, 0);
		}

		private void CheckSort(TxMempool pool, List<TxMempoolEntry> sortedSource, List<string> sortedOrder)
		{
			Assert.Equal(pool.Size, sortedOrder.Count());
			int count = 0;
			using (var it = sortedSource.GetEnumerator())
				for (; it.MoveNext(); ++count)
				{
					Assert.Equal(it.Current.TransactionHash.ToString(), sortedOrder[count]);
				}
		}

		[Fact]
		public void MempoolIndexingTest()
		{
			var pool = new TxMempool(new FeeRate(0), NodeSettings.Default());
			var entry = new TestMemPoolEntryHelper();

			/* 3rd highest fee */
			Transaction tx1 = new Transaction();
			tx1.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx1.GetHash(), entry.Fee(new Money(10000L)).Priority(10.0).FromTx(tx1));

			/* highest fee */
			Transaction tx2 = new Transaction();
			tx2.AddOutput(new TxOut(new Money(2 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx2.GetHash(), entry.Fee(new Money(20000L)).Priority(9.0).FromTx(tx2));

			/* lowest fee */
			Transaction tx3 = new Transaction();
			tx3.AddOutput(new TxOut(new Money(5 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx3.GetHash(), entry.Fee(new Money(0L)).Priority(100.0).FromTx(tx3));

			/* 2nd highest fee */
			Transaction tx4 = new Transaction();
			tx4.AddOutput(new TxOut(new Money(6 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx4.GetHash(), entry.Fee(new Money(15000L)).Priority(1.0).FromTx(tx4));

			/* equal fee rate to tx1, but newer */
			Transaction tx5 = new Transaction();
			tx5.AddOutput(new TxOut(new Money(11 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx5.GetHash(), entry.Fee(new Money(10000L)).Priority(10.0).Time(1).FromTx(tx5));

			// assert size
			Assert.Equal(pool.Size, 5);

			List<string> sortedOrder = new List<string>(5);
			sortedOrder.Insert(0, tx3.GetHash().ToString()); // 0
			sortedOrder.Insert(1, tx5.GetHash().ToString()); // 10000
			sortedOrder.Insert(2, tx1.GetHash().ToString()); // 10000
			sortedOrder.Insert(3, tx4.GetHash().ToString()); // 15000
			sortedOrder.Insert(4, tx2.GetHash().ToString()); // 20000
			CheckSort(pool,  pool.MapTx.DescendantScore.ToList(), sortedOrder);

			/* low fee but with high fee child */
			/* tx6 -> tx7 -> tx8, tx9 -> tx10 */
			Transaction tx6 = new Transaction();
			tx6.AddOutput(new TxOut(new Money(20 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx6.GetHash(), entry.Fee(new Money(0L)).FromTx(tx6));

			// assert size
			Assert.Equal(pool.Size, 6);

			// Check that at this point, tx6 is sorted low
			sortedOrder.Insert(0, tx6.GetHash().ToString());
			CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);


			TxMempool.SetEntries setAncestors = new TxMempool.SetEntries();
			setAncestors.Add(pool.MapTx.TryGet(tx6.GetHash()));
			Transaction tx7 = new Transaction();
			tx7.AddInput(new TxIn(new OutPoint(tx6.GetHash(), 0), new Script(OpcodeType.OP_11)));
			tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			tx7.AddOutput(new TxOut(new Money(1 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));

			TxMempool.SetEntries setAncestorsCalculated = new TxMempool.SetEntries();
			string dummy;
			Assert.Equal(pool.CalculateMemPoolAncestors(entry.Fee(2000000L).FromTx(tx7), setAncestorsCalculated, 100, 1000000, 1000, 1000000, out dummy), true);
			Assert.True(setAncestorsCalculated.Equals(setAncestors));

			pool.AddUnchecked(tx7.GetHash(), entry.FromTx(tx7), setAncestors);
			Assert.Equal(pool.Size, 7);

			// Now tx6 should be sorted higher (high fee child): tx7, tx6, tx2, ...
			sortedOrder.RemoveAt(0);
			sortedOrder.Add(tx6.GetHash().ToString());
			sortedOrder.Add(tx7.GetHash().ToString());
			CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

			/* low fee child of tx7 */
			Transaction tx8 = new Transaction();
			tx8.AddInput(new TxIn(new OutPoint(tx7.GetHash(), 0), new Script(OpcodeType.OP_11)));
			tx8.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			setAncestors.Add(pool.MapTx.TryGet(tx7.GetHash()));
			pool.AddUnchecked(tx8.GetHash(), entry.Fee(0L).Time(2).FromTx(tx8), setAncestors);

			// Now tx8 should be sorted low, but tx6/tx both high
			sortedOrder.Insert(0, tx8.GetHash().ToString());
			CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

			/* low fee child of tx7 */
			Transaction tx9 = new Transaction();
			tx9.AddInput(new TxIn(new OutPoint(tx7.GetHash(), 1), new Script(OpcodeType.OP_11)));
			tx9.AddOutput(new TxOut(new Money(1 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx9.GetHash(), entry.Fee(0L).Time(3).FromTx(tx9), setAncestors);

			// tx9 should be sorted low
			Assert.Equal(pool.Size, 9);

			sortedOrder.Insert(0, tx9.GetHash().ToString());
			CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

			List<string>  snapshotOrder = sortedOrder.ToList();

			setAncestors.Add(pool.MapTx.TryGet(tx8.GetHash()));
			setAncestors.Add(pool.MapTx.TryGet(tx9.GetHash()));
			/* tx10 depends on tx8 and tx9 and has a high fee*/
			Transaction tx10 = new Transaction();
			tx10.AddInput(new TxIn(new OutPoint(tx8.GetHash(), 0), new Script(OpcodeType.OP_11)));
			tx10.AddInput(new TxIn(new OutPoint(tx9.GetHash(), 0), new Script(OpcodeType.OP_11)));
			tx10.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));

			setAncestorsCalculated.Clear();
			Assert.Equal(pool.CalculateMemPoolAncestors(entry.Fee(200000L).Time(4).FromTx(tx10), setAncestorsCalculated, 100, 1000000, 1000, 1000000, out dummy), true);
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
			sortedOrder.Insert( 5, tx9.GetHash().ToString());
			sortedOrder.Insert( 6, tx8.GetHash().ToString());
			sortedOrder.Insert( 7, tx10.GetHash().ToString()); // tx10 is just before tx6
			CheckSort(pool, pool.MapTx.DescendantScore.ToList(), sortedOrder);

			// there should be 10 transactions in the mempool
			Assert.Equal(pool.Size, 10);

			// Now try removing tx10 and verify the sort order returns to normal
			pool.RemoveRecursive(pool.MapTx.TryGet(tx10.GetHash()).Transaction);
			CheckSort(pool, pool.MapTx.DescendantScore.ToList(), snapshotOrder);

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
			CheckSort(pool, pool.MapTx.MiningScore.ToList(), sortedOrder);
		}

		[Fact]
		public void MempoolAncestorIndexingTest()
		{
			var pool = new TxMempool(new FeeRate(0), NodeSettings.Default());
			var entry = new TestMemPoolEntryHelper();

			/* 3rd highest fee */
			Transaction tx1 = new Transaction();
			tx1.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx1.GetHash(), entry.Fee(new Money(10000L)).Priority(10.0).FromTx(tx1));

			/* highest fee */
			Transaction tx2 = new Transaction();
			tx2.AddOutput(new TxOut(new Money(2 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx2.GetHash(), entry.Fee(new Money(20000L)).Priority(9.0).FromTx(tx2));
			var tx2Size = tx2.GetVirtualSize();

			/* lowest fee */
			Transaction tx3 = new Transaction();
			tx3.AddOutput(new TxOut(new Money(5 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx3.GetHash(), entry.Fee(new Money(0L)).Priority(100.0).FromTx(tx3));

			/* 2nd highest fee */
			Transaction tx4 = new Transaction();
			tx4.AddOutput(new TxOut(new Money(6 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx4.GetHash(), entry.Fee(new Money(15000L)).Priority(1.0).FromTx(tx4));

			/* equal fee rate to tx1, but newer */
			Transaction tx5 = new Transaction();
			tx5.AddOutput(new TxOut(new Money(11 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx5.GetHash(), entry.Fee(new Money(10000L)).Priority(1.0).FromTx(tx5));

			// assert size
			Assert.Equal(pool.Size, 5);

			List<string> sortedOrder = new List<string>(5);
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
			CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);

			/* low fee parent with high fee child */
			/* tx6 (0) -> tx7 (high) */
			Transaction tx6 = new Transaction();
			tx6.AddOutput(new TxOut(new Money(20 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx6.GetHash(), entry.Fee(new Money(0L)).FromTx(tx6));
			var tx6Size = tx6.GetVirtualSize();
			Assert.Equal(pool.Size, 6);
			// Ties are broken by hash
			if (tx3.GetHash() < tx6.GetHash())
				sortedOrder.Add(tx6.GetHash().ToString());
			else
				sortedOrder.Insert(sortedOrder.Count - 1, tx6.GetHash().ToString());
			CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);

			Transaction tx7 = new Transaction();
			tx7.AddInput(new TxIn(new OutPoint(tx6.GetHash(), 0), new Script(OpcodeType.OP_11)));
			tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			var tx7Size = tx7.GetVirtualSize();
			Money fee = (20000 / tx2Size) * (tx7Size + tx6Size) - 1;
			pool.AddUnchecked(tx7.GetHash(), entry.Fee(fee).FromTx(tx7));
			Assert.Equal(pool.Size, 7);
			sortedOrder.Insert(1, tx7.GetHash().ToString());
			CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);

			/* after tx6 is mined, tx7 should move up in the sort */
			List<Transaction> vtx = new List<Transaction>(new[] {tx6});
			pool.RemoveForBlock(vtx, 1);

			sortedOrder.RemoveAt(1);
			// Ties are broken by hash
			if (tx3.GetHash() < tx6.GetHash())
				sortedOrder.Remove(sortedOrder.Last());
			else
				sortedOrder.RemoveAt(sortedOrder.Count - 2);
			sortedOrder.Insert(0, tx7.GetHash().ToString());
			CheckSort(pool, pool.MapTx.AncestorScore.ToList(), sortedOrder);
		}

		[Fact]
		public void MempoolSizeLimitTest()
		{
			var dateTimeSet = new DateTimeProviderSet();
			var pool = new TxMempool(new FeeRate(1000), dateTimeSet, NodeSettings.Default());
			var entry = new TestMemPoolEntryHelper();
			entry.Priority(10.0);

			Transaction tx1 = new Transaction();
			tx1.AddInput(new TxIn(new Script(OpcodeType.OP_1)));
			tx1.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_1, OpcodeType.OP_EQUAL)));
			pool.AddUnchecked(tx1.GetHash(), entry.Fee(10000L).FromTx(tx1, pool));

			Transaction tx2 = new Transaction();
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
			Transaction tx3 = new Transaction();
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

			FeeRate maxFeeRateRemoved = new FeeRate(25000, tx3.GetVirtualSize() + tx2.GetVirtualSize());
			Assert.Equal(pool.GetMinFee(1).FeePerK, maxFeeRateRemoved.FeePerK + 1000); 

			Transaction tx4 = new Transaction();
			tx4.AddInput(new TxIn(new Script(OpcodeType.OP_4)));
			tx4.AddInput(new TxIn(new Script(OpcodeType.OP_4)));
			tx4.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_4, OpcodeType.OP_EQUAL)));
			tx4.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_4, OpcodeType.OP_EQUAL)));

			Transaction tx5 = new Transaction();
			tx5.AddInput(new TxIn(new OutPoint(tx4.GetHash(), 0), new Script(OpcodeType.OP_4)));
			tx5.AddInput(new TxIn(new Script(OpcodeType.OP_5)));
			tx5.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_5, OpcodeType.OP_EQUAL)));
			tx5.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_5, OpcodeType.OP_EQUAL)));

			Transaction tx6 = new Transaction();
			tx6.AddInput(new TxIn(new OutPoint(tx4.GetHash(), 0), new Script(OpcodeType.OP_4)));
			tx6.AddInput(new TxIn(new Script(OpcodeType.OP_6)));
			tx6.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_6, OpcodeType.OP_EQUAL)));
			tx6.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_6, OpcodeType.OP_EQUAL)));

			Transaction tx7 = new Transaction();
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

			List<Transaction> vtx = new List<Transaction>();
			dateTimeSet.time = 42 + TxMempool.RollingFeeHalflife ;
			Assert.Equal(pool.GetMinFee(1).FeePerK.Satoshi, maxFeeRateRemoved.FeePerK.Satoshi + 1000);
			// ... we should keep the same min fee until we get a block
			pool.RemoveForBlock(vtx, 1);
			dateTimeSet.time = 42 + 2*+TxMempool.RollingFeeHalflife;
			Assert.Equal(pool.GetMinFee(1).FeePerK.Satoshi, (maxFeeRateRemoved.FeePerK.Satoshi + 1000) / 2);
			// ... then feerate should drop 1/2 each halflife

			dateTimeSet.time = 42 + 2 * TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife / 2;
			Assert.Equal(pool.GetMinFee(pool.DynamicMemoryUsage() * 5 / 2).FeePerK.Satoshi, (maxFeeRateRemoved.FeePerK.Satoshi + 1000) / 4);
			// ... with a 1/2 halflife when mempool is < 1/2 its target size

			dateTimeSet.time = 42 + 2*TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife/2 + TxMempool.RollingFeeHalflife/4 ;
			Assert.Equal(pool.GetMinFee(pool.DynamicMemoryUsage() * 9 / 2).FeePerK.Satoshi, (maxFeeRateRemoved.FeePerK.Satoshi + 1000) / 8);
			// ... with a 1/4 halflife when mempool is < 1/4 its target size

			dateTimeSet.time = 42 + 7* TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife/2 + TxMempool.RollingFeeHalflife/4 ;
			Assert.Equal(pool.GetMinFee(1).FeePerK.Satoshi, 1000);
			// ... but feerate should never drop below 1000

			dateTimeSet.time = 42 + 8* TxMempool.RollingFeeHalflife + TxMempool.RollingFeeHalflife/2 + TxMempool.RollingFeeHalflife/4 ;
			Assert.Equal(pool.GetMinFee(1).FeePerK, 0);
			// ... unless it has gone all the way to 0 (after getting past 1000/2)
		}

		public class DateTimeProviderSet : DateTimeProvider
		{
			public long time;
			public DateTime timeutc;

			public override long GetTime()
			{
				return time;
			}

			public override DateTime GetUtcNow()
			{
				return timeutc;
			}
		}

		[Fact]
		public void MempoolConcurrencyTest()
		{
			var pool = new TxMempool(new FeeRate(1000), NodeSettings.Default());
			var scheduler = new AsyncLock();
			var rand = new Random();

			var value = 10000;
			List<Transaction> txs = new List<Transaction>();
			for (int i = 0; i < 20; i++)
			{
				var tx = new Transaction();
				tx.AddInput(new TxIn(new Script(OpcodeType.OP_11)));
				tx.AddOutput(new TxOut(new Money(value++), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
				txs.Add(tx);
			}

			List<Task> tasks = new List<Task>();
			var options = new ParallelOptions {MaxDegreeOfParallelism = 10};
			Parallel.ForEach(txs, options, transaction =>
			{
				var entry = new TxMempoolEntry(transaction, new Money(rand.Next(100)), 0, 0.0, 1, transaction.TotalOut, false, 4, new LockPoints());
				tasks.Add(scheduler.WriteAsync(() => pool.AddUnchecked(transaction.GetHash(), entry)));
			});

			Task.WaitAll(tasks.ToArray());
			Assert.Equal(scheduler.ReadAsync(() => pool.Size).Result, 20);
		}

		[Fact]
		public void AddToMempool()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNodeSync = builder.CreateStratisNode();
				builder.StartAll();

				stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
				stratisNodeSync.GenerateStratis(105); // coinbase maturity = 100
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

				var block = stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
				var prevTrx = block.Transactions.First();
				var dest = new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network);

				Transaction tx = new Transaction();
				tx.AddInput(new TxIn(new OutPoint(prevTrx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey)));
				tx.AddOutput(new TxOut("25", dest.PubKey.Hash));
				tx.AddOutput(new TxOut("24", new Key().PubKey.Hash)); // 1 btc fee
				tx.Sign(stratisNodeSync.MinerSecret, false);

				stratisNodeSync.Broadcast(tx);

				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
			}
		}

		[Fact]
		public void AddToMempoolTrxSpendingTwoOutputFromSameTrx()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNodeSync = builder.CreateStratisNode();
				builder.StartAll();
				stratisNodeSync.NotInIBD();

				stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
				stratisNodeSync.GenerateStratis(105); // coinbase maturity = 100
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

				var block = stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(4).HashBlock).Result;
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
				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
				// mine the transactions in the mempool
				stratisNodeSync.GenerateStratis(1, stratisNodeSync.FullNode.MempoolManager.InfoAllAsync().Result.Select(s => s.Trx).ToList());
				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 0);
				
				//create a new trx spending both outputs
				Transaction tx = new Transaction();
				tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 0), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest1.PubKey)));
				tx.AddInput(new TxIn(new OutPoint(parentTx.GetHash(), 1), PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(dest2.PubKey)));
				tx.AddOutput(new TxOut("48", new Key().PubKey.Hash)); // 1 btc fee
				var signed = new TransactionBuilder().AddKeys(dest1, dest2).AddCoins(parentTx.Outputs.AsCoins()).SignTransaction(tx);

				stratisNodeSync.Broadcast(signed);
				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 1);
			}
		}

		[Fact]
		public void MempoolReceiveFromManyNodes()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNodeSync = builder.CreateStratisNode();
				builder.StartAll();
				stratisNodeSync.NotInIBD();

				stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
				stratisNodeSync.GenerateStratis(201); // coinbase maturity = 100
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

				var trxs = new List<Transaction>();
				foreach (var index in Enumerable.Range(1, 100))
				{
					var block = stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
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

				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 100);
			}
		}

		[Fact]
		public void TxMempoolBlockDoublespend()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNodeSync = builder.CreateStratisNode();
				builder.StartAll();
				stratisNodeSync.NotInIBD();
				stratisNodeSync.FullNode.Settings.RequireStandard = true; // make sure to test standard tx

				stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
				stratisNodeSync.GenerateStratis(100); // coinbase maturity = 100
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

				// Make sure skipping validation of transctions that were
				// validated going into the memory pool does not allow
				// double-spends in blocks to pass validation when they should not.

				var scriptPubKey = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(stratisNodeSync.MinerSecret.PubKey);
				var genBlock = stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;

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
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());

				// Test 2: ... and should be rejected if spend1 is in the memory pool
				Assert.True(stratisNodeSync.AddToStratisMempool(spends[0]));
				block = stratisNodeSync.GenerateStratis(1, spends).Single();
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());
				stratisNodeSync.FullNode.MempoolManager.Clear().Wait();

				// Test 3: ... and should be rejected if spend2 is in the memory pool
				Assert.True(stratisNodeSync.AddToStratisMempool(spends[1]));
				block = stratisNodeSync.GenerateStratis(1, spends).Single();
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock != block.GetHash());
				stratisNodeSync.FullNode.MempoolManager.Clear().Wait();

				// Final sanity test: first spend in mempool, second in block, that's OK:
				List<Transaction> oneSpend = new List<Transaction>();
				oneSpend.Add(spends[0]);
				Assert.True(stratisNodeSync.AddToStratisMempool(spends[1]));
				block = stratisNodeSync.GenerateStratis(1, oneSpend).Single();
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Assert.True(stratisNodeSync.FullNode.Chain.Tip.HashBlock == block.GetHash());

				// spends[1] should have been removed from the mempool when the
				// block with spends[0] is accepted:
				Class1.Eventually(() => stratisNodeSync.FullNode.MempoolManager.MempoolSize().Result == 0);
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
				var stratisNode = builder.CreateStratisNode();
				builder.StartAll();

				stratisNode.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNode.FullNode.Network));

				// 50 orphan transactions:
				for (ulong i = 0; i < 50; i++)
				{
					Transaction tx = new Transaction();
					tx.AddInput(new TxIn(new OutPoint(randHash(), 0), new Script(OpcodeType.OP_1)));
					tx.AddOutput(new TxOut(new Money(1*Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
					
					stratisNode.FullNode.MempoolManager.Orphans.AddOrphanTx(i, tx).Wait();
				}

				Assert.Equal(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count, 50);

				// ... and 50 that depend on other orphans:
				for (ulong i = 0; i < 50; i++)
				{
					var txPrev = stratisNode.FullNode.MempoolManager.Orphans.OrphansList().ElementAt(rand.Next(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count));

					Transaction tx = new Transaction();
					tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), 0), new Script(OpcodeType.OP_1)));
					tx.AddOutput(new TxOut(new Money((1 + i + 100) * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
					stratisNode.FullNode.MempoolManager.Orphans.AddOrphanTx(i, tx).Wait();
				}

				Assert.Equal(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count, 100);

				// This really-big orphan should be ignored:
				for (ulong i = 0; i < 10; i++)
				{
					var txPrev = stratisNode.FullNode.MempoolManager.Orphans.OrphansList().ElementAt(rand.Next(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count));
					Transaction tx = new Transaction();
					tx.AddOutput(new TxOut(new Money(1 * Money.CENT), stratisNode.MinerSecret.ScriptPubKey));
					foreach (var index in Enumerable.Range(0, 2777))
						tx.AddInput(new TxIn(new OutPoint(txPrev.Tx.GetHash(), index), new Script(OpcodeType.OP_1)));

					Assert.False(stratisNode.FullNode.MempoolManager.Orphans.AddOrphanTx(i, tx).Result);
				}

				Assert.Equal(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count, 100);

				// Test EraseOrphansFor:
				for (ulong i = 0; i < 3; i++)
				{
					var sizeBefore = stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count;
					stratisNode.FullNode.MempoolManager.Orphans.EraseOrphansFor(i).Wait();
					Assert.True(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count < sizeBefore);
				}

				// Test LimitOrphanTxSize() function:
				stratisNode.FullNode.MempoolManager.Orphans.LimitOrphanTxSize(40).Wait();
				Assert.True(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count <= 40);
				stratisNode.FullNode.MempoolManager.Orphans.LimitOrphanTxSize(10).Wait();
				Assert.True(stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Count <= 10);
				stratisNode.FullNode.MempoolManager.Orphans.LimitOrphanTxSize(0).Wait();
				Assert.True(!stratisNode.FullNode.MempoolManager.Orphans.OrphansList().Any());
			}
		}

		[Fact]
		public void MempoolAddNodeWithOrphans()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNodeSync = builder.CreateStratisNode();
				builder.StartAll();
				stratisNodeSync.NotInIBD();

				stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
				stratisNodeSync.GenerateStratis(101); // coinbase maturity = 100
				Class1.Eventually(() => stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

				var block = stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(1).HashBlock).Result;
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
				Class1.Eventually(() => stratisNodeSync.FullNode.MempoolManager.Orphans.OrphansList().Count == 1);
				// broadcast the parent
				stratisNodeSync.Broadcast(tx);
				Class1.Eventually(() => stratisNodeSync.FullNode.MempoolManager.Orphans.OrphansList().Count == 0);
				// wait for orphan to get in the pool
				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 2);
			}
		}

		[Fact]
		public void MempoolSyncTransactions()
		{
			using (NodeBuilder builder = NodeBuilder.Create())
			{
				var stratisNodeSync = builder.CreateStratisNode();
				var stratisNode1 = builder.CreateStratisNode();
				var stratisNode2 = builder.CreateStratisNode();
				builder.StartAll();

				// not in IBD
				stratisNodeSync.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
				stratisNode1.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));
				stratisNode2.FullNode.ChainBehaviorState.SetIsInitialBlockDownload(false, DateTime.UtcNow.AddMinutes(5));

				// generate blocks and wait for the downloader to pickup
				stratisNodeSync.SetDummyMinerSecret(new BitcoinSecret(new Key(), stratisNodeSync.FullNode.Network));
				stratisNodeSync.GenerateStratis(105); // coinbase maturity = 100
				// wait for block repo for block sync to work
				Class1.Eventually(() => stratisNodeSync.FullNode.Chain.Tip.HashBlock == stratisNodeSync.FullNode.ConsensusLoop.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestValidatedPoW.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);
				Class1.Eventually(() => stratisNodeSync.FullNode.ChainBehaviorState.HighestPersistedBlock.HashBlock == stratisNodeSync.FullNode.Chain.Tip.HashBlock);

				Class1.Eventually(() => stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.CreateRPCClient().GetBestBlockHash()).Result != null); 

				// sync both nodes
				stratisNode1.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
				stratisNode2.CreateRPCClient().AddNode(stratisNodeSync.Endpoint, true);
				Class1.Eventually(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
				Class1.Eventually(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());

				// create some transactions and push them to the pool
				var trxs = new List<Transaction>();
				foreach (var index in Enumerable.Range(1, 5))
				{
					var block = stratisNodeSync.FullNode.BlockStoreManager.BlockRepository.GetAsync(stratisNodeSync.FullNode.Chain.GetBlock(index).HashBlock).Result;
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
				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 5);

				// the full node should be connected to both nodes
				Assert.Equal(stratisNodeSync.FullNode.ConnectionManager.ConnectedNodes.Count, 2);

				// reset the trickle timer on the full node that has the transactions in the pool
				foreach (var node in stratisNodeSync.FullNode.ConnectionManager.ConnectedNodes) node.Behavior<MempoolBehavior>().NextInvSend = 0;

				Class1.Eventually(() => stratisNode1.CreateRPCClient().GetRawMempool().Length == 5);
				Class1.Eventually(() => stratisNode2.CreateRPCClient().GetRawMempool().Length == 5);

				// mine the transactions in the mempool
				stratisNodeSync.GenerateStratis(1, stratisNodeSync.FullNode.MempoolManager.InfoAllAsync().Result.Select(s => s.Trx).ToList());
				Class1.Eventually(() => stratisNodeSync.CreateRPCClient().GetRawMempool().Length == 0);

				// wait for block and mempool to change
				Class1.Eventually(() => stratisNode1.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash()); 
				Class1.Eventually(() => stratisNode2.CreateRPCClient().GetBestBlockHash() == stratisNodeSync.CreateRPCClient().GetBestBlockHash());
				Class1.Eventually(() => stratisNode1.CreateRPCClient().GetRawMempool().Length == 0);
				Class1.Eventually(() => stratisNode2.CreateRPCClient().GetRawMempool().Length == 0);
			}
		}
	}

	public class TestMemPoolEntryHelper
	{
		// Default values
		Money nFee = Money.Zero;
		long nTime = 0;
		double dPriority = 0.0;
		int nHeight = 1;
		bool spendsCoinbase = false;
		long sigOpCost = 4;
		LockPoints lp;


		public TxMempoolEntry FromTx(Transaction tx, TxMempool pool = null)
		{
			Money inChainValue = (pool != null && pool.HasNoInputsOf(tx)) ? tx.TotalOut : 0;

			return new TxMempoolEntry(tx, nFee, nTime, dPriority, nHeight,
				inChainValue, spendsCoinbase, sigOpCost, lp);

		}

		// Change the default value
		public TestMemPoolEntryHelper Fee(Money _fee) { nFee = _fee; return this; }
		public TestMemPoolEntryHelper Time(long _time) { nTime = _time; return this; }
		public TestMemPoolEntryHelper Priority(double _priority) { dPriority = _priority; return this; }
		public TestMemPoolEntryHelper Height(int _height) { nHeight = _height; return this; }
		public TestMemPoolEntryHelper SpendsCoinbase(bool _flag) { spendsCoinbase = _flag; return this; }
		public TestMemPoolEntryHelper SigOpsCost(long _sigopsCost) { sigOpCost = _sigopsCost; return this; }

	}
}