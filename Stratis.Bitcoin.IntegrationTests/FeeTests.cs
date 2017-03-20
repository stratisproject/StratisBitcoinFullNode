using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using NBitcoin.Protocol;
using Stratis.Bitcoin.BlockStore;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.MemoryPool.Fee;
using Stratis.Bitcoin.MemoryPool;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class FeeTests
	{
		[Fact]
		public void BlockPolicyEstimates()
	    {
			var dateTimeSet = new MemoryPoolTests.DateTimeProviderSet();
		    TxMempool mpool = new TxMempool(new FeeRate(1000), NodeSettings.Default());
			TestMemPoolEntryHelper entry = new TestMemPoolEntryHelper();
			Money basefee = new Money(2000);
			Money deltaFee = new Money(100);
			List<Money> feeV = new List<Money>();

			// Populate vectors of increasing fees
			for (int j = 0; j < 10; j++)
			{
				feeV.Add(basefee * (j + 1));
			}

			// Store the hashes of transactions that have been
			// added to the mempool by their associate fee
			// txHashes[j] is populated with transactions either of
			// fee = basefee * (j+1)
			List<uint256>[] txHashes = new List<uint256>[10];
		    for (int i = 0; i < txHashes.Length; i++) txHashes[i] = new List<uint256>();

			// Create a transaction template
		    Script garbage = new Script(Enumerable.Range(0, 128).Select(i => (byte)1).ToArray());
		
			Transaction txf = new Transaction();
			txf.AddInput(new TxIn(garbage));
			txf.AddOutput(new TxOut(0L, Script.Empty));
			FeeRate baseRate = new FeeRate(basefee, txf.GetVirtualSize());

			// Create a fake block
			List<Transaction> block = new List<Transaction>();
			int blocknum = 0;
			int answerFound;

			// Loop through 200 blocks
			// At a decay .998 and 4 fee transactions per block
			// This makes the tx count about 1.33 per bucket, above the 1 threshold
			while (blocknum < 200)
			{
				for (int j = 0; j < 10; j++)
				{ // For each fee
					for (int k = 0; k < 4; k++)
					{ // add 4 fee txs
						var tx = txf.Clone(false);
						tx.Inputs[0].PrevOut.N = (uint)(10000 * blocknum + 100 * j + k); // make transaction unique
						uint256 hash = tx.GetHash();
						mpool.AddUnchecked(hash, entry.Fee(feeV[j]).Time(dateTimeSet.GetTime()).Priority(0).Height(blocknum).FromTx(tx, mpool));
						txHashes[j].Add(hash);
					}
				}
				//Create blocks where higher fee txs are included more often
				for (int h = 0; h <= blocknum % 10; h++)
				{
					// 10/10 blocks add highest fee transactions
					// 9/10 blocks add 2nd highest and so on until ...
					// 1/10 blocks add lowest fee transactions
					while (txHashes[9 - h].Count > 0)
					{
						var ptx = mpool.Get(txHashes[9 - h].Last());
						if (ptx != null)
							block.Add(ptx);
						txHashes[9 - h].Remove(txHashes[9 - h].Last());
					}
				}
				mpool.RemoveForBlock(block, ++blocknum);
				block.Clear();
				if (blocknum == 30)
				{
					// At this point we should need to combine 5 buckets to get enough data points
					// So estimateFee(1,2,3) should fail and estimateFee(4) should return somewhere around
					// 8*baserate.  estimateFee(4) %'s are 100,100,100,100,90 = average 98%
					Assert.True(mpool.EstimateFee(1) == new FeeRate(0));
					Assert.True(mpool.EstimateFee(2) == new FeeRate(0));
					Assert.True(mpool.EstimateFee(3) == new FeeRate(0));
					Assert.True(mpool.EstimateFee(4).FeePerK < 8 * baseRate.FeePerK + deltaFee);
					Assert.True(mpool.EstimateFee(4).FeePerK > 8 * baseRate.FeePerK - deltaFee);
					
					Assert.True(mpool.EstimateSmartFee(1, out answerFound) == mpool.EstimateFee(4) && answerFound == 4);
					Assert.True(mpool.EstimateSmartFee(3, out answerFound) == mpool.EstimateFee(4) && answerFound == 4);
					Assert.True(mpool.EstimateSmartFee(4, out answerFound) == mpool.EstimateFee(4) && answerFound == 4);
					Assert.True(mpool.EstimateSmartFee(8, out answerFound) == mpool.EstimateFee(8) && answerFound == 8);
				}
			}

			List<Money> origFeeEst = new List<Money>();
			// Highest feerate is 10*baseRate and gets in all blocks,
			// second highest feerate is 9*baseRate and gets in 9/10 blocks = 90%,
			// third highest feerate is 8*base rate, and gets in 8/10 blocks = 80%,
			// so estimateFee(1) would return 10*baseRate but is hardcoded to return failure
			// Second highest feerate has 100% chance of being included by 2 blocks,
			// so estimateFee(2) should return 9*baseRate etc...
			for (int i = 1; i < 10; i++)
			{
				origFeeEst.Add(mpool.EstimateFee(i).FeePerK);
				if (i > 2)
				{ // Fee estimates should be monotonically decreasing
					Assert.True(origFeeEst[i - 1] <= origFeeEst[i - 2]);
				}
				int mult = 11 - i;
				if (i > 1)
				{
					Assert.True(origFeeEst[i - 1] < mult * baseRate.FeePerK + deltaFee);
					Assert.True(origFeeEst[i - 1] > mult * baseRate.FeePerK - deltaFee);
				}
				else
				{
					Assert.True(origFeeEst[i - 1] == new FeeRate(0).FeePerK);
				}
			}

			// Mine 50 more blocks with no transactions happening, estimates shouldn't change
			// We haven't decayed the moving average enough so we still have enough data points in every bucket
			while (blocknum < 250)
				mpool.RemoveForBlock(block, ++blocknum);

			Assert.True(mpool.EstimateFee(1) == new FeeRate(0));
			for (int i = 2; i < 10; i++)
			{
				Assert.True(mpool.EstimateFee(i).FeePerK < origFeeEst[i - 1] + deltaFee);
				Assert.True(mpool.EstimateFee(i).FeePerK > origFeeEst[i - 1] - deltaFee);
			}


			// Mine 15 more blocks with lots of transactions happening and not getting mined
			// Estimates should go up
			while (blocknum < 265)
			{
				for (int j = 0; j < 10; j++)
				{ // For each fee multiple
					for (int k = 0; k < 4; k++)
					{ // add 4 fee txs
						var tx = txf.Clone(false);
						tx.Inputs[0].PrevOut.N = (uint)(10000 * blocknum + 100 * j + k);
						uint256 hash = tx.GetHash();
						mpool.AddUnchecked(hash, entry.Fee(feeV[j]).Time(dateTimeSet.GetTime()).Priority(0).Height(blocknum).FromTx(tx, mpool));
						txHashes[j].Add(hash);
					}
				}
				mpool.RemoveForBlock(block, ++blocknum);
			}

			for (int i = 1; i < 10; i++)
			{
				Assert.True(mpool.EstimateFee(i) == new FeeRate(0) || mpool.EstimateFee(i).FeePerK > origFeeEst[i - 1] - deltaFee);
				Assert.True(mpool.EstimateSmartFee(i, out answerFound).FeePerK > origFeeEst[answerFound - 1] - deltaFee);
			}

			// Mine all those transactions
			// Estimates should still not be below original
			for (int j = 0; j < 10; j++)
			{
				while (txHashes[j].Count > 0)
				{
					var ptx = mpool.Get(txHashes[j].Last());
					if (ptx != null)
						block.Add(ptx);
					txHashes[j].Remove(txHashes[j].Last());
				}
			}
			mpool.RemoveForBlock(block, 265);
			block.Clear();
			Assert.True(mpool.EstimateFee(1) == new FeeRate(0));
			for (int i = 2; i < 10; i++)
			{
				Assert.True(mpool.EstimateFee(i).FeePerK > origFeeEst[i - 1] - deltaFee);
			}

			// Mine 200 more blocks where everything is mined every block
			// Estimates should be below original estimates
			while (blocknum < 465)
			{
				for (int j = 0; j < 10; j++)
				{ // For each fee multiple
					for (int k = 0; k < 4; k++)
					{ // add 4 fee txs
						var tx = txf.Clone(false);
						tx.Inputs[0].PrevOut.N = (uint)(10000 * blocknum + 100 * j + k);
						uint256 hash = tx.GetHash();
						mpool.AddUnchecked(hash, entry.Fee(feeV[j]).Time(dateTimeSet.GetTime()).Priority(0).Height(blocknum).FromTx(tx, mpool));
						var ptx = mpool.Get(hash);
						if (ptx != null)
							block.Add(ptx);

					}
				}
				mpool.RemoveForBlock(block, ++blocknum);
				block.Clear();
			}
			Assert.True(mpool.EstimateFee(1) ==  new FeeRate(0));
			for (int i = 2; i < 10; i++)
			{
				Assert.True(mpool.EstimateFee(i).FeePerK < origFeeEst[i - 1] - deltaFee);
			}

			// Test that if the mempool is limited, estimateSmartFee won't return a value below the mempool min fee
			// and that estimateSmartPriority returns essentially an infinite value
			mpool.AddUnchecked(txf.GetHash(), entry.Fee(feeV[5]).Time(dateTimeSet.GetTime()).Priority(0).Height(blocknum).FromTx(txf, mpool));
			// evict that transaction which should set a mempool min fee of minRelayTxFee + feeV[5]
			mpool.TrimToSize(1);
			Assert.True(mpool.GetMinFee(1).FeePerK > feeV[5]);
			for (int i = 1; i < 10; i++)
			{
				Assert.True(mpool.EstimateSmartFee(i, out answerFound).FeePerK >= mpool.EstimateFee(i).FeePerK);
				Assert.True(mpool.EstimateSmartFee(i, out answerFound).FeePerK >= mpool.GetMinFee(1).FeePerK);
				Assert.True(mpool.EstimateSmartPriority(i, out answerFound) == BlockPolicyEstimator.INF_PRIORITY);
			}
		}
    }
}
