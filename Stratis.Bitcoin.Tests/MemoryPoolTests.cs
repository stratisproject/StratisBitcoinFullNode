using System;
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
			TestMemPoolEntryHelper Fee(Money _fee) { nFee = _fee; return this; }
			TestMemPoolEntryHelper Time(long _time) { nTime = _time; return this; }
			TestMemPoolEntryHelper Priority(double _priority) { dPriority = _priority; return this; }
			TestMemPoolEntryHelper Height(int _height) { nHeight = _height; return this; }
			TestMemPoolEntryHelper SpendsCoinbase(bool _flag) { spendsCoinbase = _flag; return this; }
			TestMemPoolEntryHelper SigOpsCost(long _sigopsCost) { sigOpCost = _sigopsCost; return this; }

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
    }
}
