﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.MemoryPool;
using Xunit;

namespace Stratis.Bitcoin.Tests
{
    public class MemoryPoolTests
	{
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


			public TxMemPoolEntry FromTx(Transaction tx, TxMemPool pool = null)
			{
				Money inChainValue = (pool != null && pool.HasNoInputsOf(tx)) ? tx.TotalOut : 0;

				return new TxMemPoolEntry(tx, nFee, nTime, dPriority, nHeight,
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

			TxMemPool testPool = new TxMemPool(new FeeRate(0));

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

		private void CheckSort(TxMemPool pool, List<TxMemPoolEntry> sortedSource, List<string> sortedOrder)
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
			
			//Transaction xxx = new Transaction();
			//xxx.AddOutput(new TxOut(new Money(000 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			//pool.AddUnchecked(xxx.GetHash(), entry.Fee(new Money(000L)).Priority(000.0).FromTx(xxx));

			var pool = new TxMemPool(new FeeRate(0));
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


			TxMemPool.SetEntries setAncestors = new TxMemPool.SetEntries();
			setAncestors.Add(pool.MapTx.TryGet(tx6.GetHash()));
			Transaction tx7 = new Transaction();
			tx7.AddInput(new TxIn(new OutPoint(tx6.GetHash(), 0), new Script(OpcodeType.OP_11)));
			tx7.AddOutput(new TxOut(new Money(10 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));
			tx7.AddOutput(new TxOut(new Money(1 * Money.COIN), new Script(OpcodeType.OP_11, OpcodeType.OP_EQUAL)));

			TxMemPool.SetEntries setAncestorsCalculated = new TxMemPool.SetEntries();
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

			//pool.removeRecursive(pool.MapTx.find(tx9.GetHash())->GetTx());
			//pool.removeRecursive(pool.MapTx.find(tx8.GetHash())->GetTx());
			///* Now check the sort on the mining score index.
			// * Final order should be:
			// *
			// * tx7 (2M)
			// * tx2 (20k)
			// * tx4 (15000)
			// * tx1/tx5 (10000)
			// * tx3/6 (0)
			// * (Ties resolved by hash)
			// */
			//sortedOrder.clear();
			//sortedOrder.push_back(tx7.GetHash().ToString());
			//sortedOrder.push_back(tx2.GetHash().ToString());
			//sortedOrder.push_back(tx4.GetHash().ToString());
			//if (tx1.GetHash() < tx5.GetHash())
			//{
			//	sortedOrder.push_back(tx5.GetHash().ToString());
			//	sortedOrder.push_back(tx1.GetHash().ToString());
			//}
			//else
			//{
			//	sortedOrder.push_back(tx1.GetHash().ToString());
			//	sortedOrder.push_back(tx5.GetHash().ToString());
			//}
			//if (tx3.GetHash() < tx6.GetHash())
			//{
			//	sortedOrder.push_back(tx6.GetHash().ToString());
			//	sortedOrder.push_back(tx3.GetHash().ToString());
			//}
			//else
			//{
			//	sortedOrder.push_back(tx3.GetHash().ToString());
			//	sortedOrder.push_back(tx6.GetHash().ToString());
			//}
			//CheckSort<mining_score>(pool, sortedOrder);
		}
	}
}
