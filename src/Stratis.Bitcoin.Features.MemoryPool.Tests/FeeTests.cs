using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.MemoryPool.Tests
{
    public class FeeTests
    {
        [Fact]
        public void BlockPolicyEstimates()
        {
            var dateTimeSet = new DateTimeProviderSet();
            var settings = NodeSettings.Default();
            TxMempool mpool = new TxMempool(DateTimeProvider.Default,
                new BlockPolicyEstimator(settings.LoggerFactory, settings), settings.LoggerFactory, settings);
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
            // At a decay .9952 and 4 fee transactions per block
            // This makes the tx count about 2.5 per bucket, well above the 0.1 threshold
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
                // Check after just a few txs that combining buckets works as expected
                if (blocknum == 3)
                {
                    // At this point we should need to combine 3 buckets to get enough data points
                    // So estimateFee(1) should fail and estimateFee(2) should return somewhere around
                    // 9*baserate.  estimateFee(2) %'s are 100,100,90 = average 97%
                    Assert.True(mpool.EstimateFee(1) == new FeeRate(0));
                    Assert.True(mpool.EstimateFee(2).FeePerK < 9 * baseRate.FeePerK + deltaFee);
                    Assert.True(mpool.EstimateFee(2).FeePerK > 9 * baseRate.FeePerK - deltaFee);
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
                if (i % 2 == 0) //At scale 2, test logic is only correct for even targets
                {
                    Assert.True(origFeeEst[i - 1] < mult * baseRate.FeePerK + deltaFee);
                    Assert.True(origFeeEst[i - 1] > mult * baseRate.FeePerK - deltaFee);
                }
            }
            // Fill out rest of the original estimates
            for (int i = 10; i <= 48; i++)
            {
                origFeeEst.Add(mpool.EstimateFee(i).FeePerK);
            }

            // Mine 50 more blocks with no transactions happening, estimates shouldn't change
            // We haven't decayed the moving average enough so we still have enough data points in every bucket
            while (blocknum < 250)
                mpool.RemoveForBlock(block, ++blocknum);

            Assert.True(mpool.EstimateFee(1) == new FeeRate(0));
            for (int i = 2; i < 9; i++)
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

            for (int i = 1; i < 9; i++)
            {
                Assert.True(mpool.EstimateFee(i) == new FeeRate(0) || mpool.EstimateFee(i).FeePerK > origFeeEst[i - 1] - deltaFee);
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
            for (int i = 2; i < 9; i++)
            {
                Assert.True(mpool.EstimateFee(i) == new FeeRate(0) || 
                    mpool.EstimateFee(i).FeePerK > origFeeEst[i - 1] - deltaFee);
            }

            // Mine 600 more blocks where everything is mined every block
            // Estimates should be below original estimates
            while (blocknum < 865)
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
            Assert.True(mpool.EstimateFee(1) == new FeeRate(0));
            for (int i = 2; i < 9; i++)
            {
                Assert.True(mpool.EstimateFee(i).FeePerK < origFeeEst[i - 1] - deltaFee);
            }            
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
    }
}
