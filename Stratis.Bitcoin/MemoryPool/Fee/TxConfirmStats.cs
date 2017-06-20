using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.MemoryPool.Fee
{
    public class TxConfirmStats
    {
        // Sum the total feerate of all tx's in each bucket
        // Track the historical moving average of this total over blocks
        private List<double> avg;

        private Dictionary<double, int> bucketMap; // Map of bucket upper-bound to index into all vectors by bucket

        //Define the buckets we will group transactions into
        private List<double> buckets; // The upper-bound of the range for the bucket (inclusive)

        // Count the total # of txs confirmed within Y blocks in each bucket
        // Track the historical moving average of theses totals over blocks
        private List<List<double>> confAvg; // confAvg[Y][X]

        // and calculate the totals for the current block to update the moving averages
        private List<List<int>> curBlockConf; // curBlockConf[Y][X]

        // and calculate the total for the current block to update the moving average
        private List<int> curBlockTxCt;

        // and calculate the total for the current block to update the moving average
        private List<double> curBlockVal;

        // Combine the conf counts with tx counts to calculate the confirmation % for each Y,X
        // Combine the total value with the tx counts to calculate the avg feerate per bucket

        private double decay;

        // transactions still unconfirmed after MAX_CONFIRMS for each bucket
        private List<int> oldUnconfTxs;

        // For each bucket X:
        // Count the total # of txs in each bucket
        // Track the historical moving average of this total over blocks
        private List<double> txCtAvg;

        // Mempool counts of outstanding transactions
        // For each bucket X, track the number of transactions in the mempool
        // that are unconfirmed for each possible confirmation value Y
        private List<List<int>> unconfTxs; //unconfTxs[Y][X]

        // Initialize the data structures.  This is called by BlockPolicyEstimator's
        // constructor with default values.
        // @param defaultBuckets contains the upper limits for the bucket boundaries
        // @param maxConfirms max number of confirms to track
        // @param decay how much to decay the historical moving average per block

        public void Initialize(List<double> defaultBuckets, int maxConfirms, double decay)
        {
            this.buckets = new List<double>();
            this.bucketMap = new Dictionary<double, int>();

            this.decay = decay;
            for (var i = 0; i < defaultBuckets.Count; i++)
            {
                this.buckets.Add(defaultBuckets[i]);
                this.bucketMap[defaultBuckets[i]] = i;
            }
            this.confAvg = new List<List<double>>();
            this.curBlockConf = new List<List<int>>();
            this.unconfTxs = new List<List<int>>();

            for (var i = 0; i < maxConfirms; i++)
            {
                this.confAvg.Insert(i, Enumerable.Repeat(default(double), this.buckets.Count).ToList());
                this.curBlockConf.Insert(i, Enumerable.Repeat(default(int), this.buckets.Count).ToList());
                this.unconfTxs.Insert(i, Enumerable.Repeat(default(int), this.buckets.Count).ToList());
            }

            this.oldUnconfTxs = new List<int>(Enumerable.Repeat(default(int), this.buckets.Count));
            this.curBlockTxCt = new List<int>(Enumerable.Repeat(default(int), this.buckets.Count));
            this.txCtAvg = new List<double>(Enumerable.Repeat(default(double), this.buckets.Count));
            this.curBlockVal = new List<double>(Enumerable.Repeat(default(double), this.buckets.Count));
            this.avg = new List<double>(Enumerable.Repeat(default(double), this.buckets.Count));
        }

        // Clear the state of the curBlock variables to start counting for the new block 
        public void ClearCurrent(int nBlockHeight)
        {
            for (var j = 0; j < this.buckets.Count; j++)
            {
                this.oldUnconfTxs[j] += this.unconfTxs[nBlockHeight % this.unconfTxs.Count][j];
                this.unconfTxs[nBlockHeight % this.unconfTxs.Count][j] = 0;
                for (var i = 0; i < this.curBlockConf.Count; i++)
                    this.curBlockConf[i][j] = 0;
                this.curBlockTxCt[j] = 0;
                this.curBlockVal[j] = 0;
            }
        }


        // Record a new transaction data point in the current block stats
        // @param blocksToConfirm the number of blocks it took this transaction to confirm
        // @param val the feerate of the transaction
        // @warning blocksToConfirm is 1-based and has to be >= 1
        public void Record(int blocksToConfirm, double val)
        {
            // blocksToConfirm is 1-based
            if (blocksToConfirm < 1)
                return;
            var bucketindex = this.bucketMap.FirstOrDefault(k => k.Key > val).Value;
            for (var i = blocksToConfirm; i <= this.curBlockConf.Count; i++)
                this.curBlockConf[i - 1][bucketindex]++;
            this.curBlockTxCt[bucketindex]++;
            this.curBlockVal[bucketindex] += val;
        }

        // Record a new transaction entering the mempool
        public int NewTx(int nBlockHeight, double val)
        {
            var bucketindex = this.bucketMap.FirstOrDefault(k => k.Key > val).Value;
            var blockIndex = nBlockHeight % this.unconfTxs.Count;
            this.unconfTxs[blockIndex][bucketindex]++;
            return bucketindex;
        }

        // Remove a transaction from mempool tracking stats
        public void RemoveTx(int entryHeight, int nBestSeenHeight, int bucketIndex)
        {
            //nBestSeenHeight is not updated yet for the new block
            var blocksAgo = nBestSeenHeight - entryHeight;
            if (nBestSeenHeight == 0) // the BlockPolicyEstimator hasn't seen any blocks yet
                blocksAgo = 0;
            if (blocksAgo < 0)
            {
                Logs.EstimateFee.LogInformation($"Blockpolicy error, blocks ago is negative for mempool tx");
                return; //This can't happen because we call this with our best seen height, no entries can have higher
            }

            if (blocksAgo >= this.unconfTxs.Count)
            {
                if (this.oldUnconfTxs[bucketIndex] > 0)
                    this.oldUnconfTxs[bucketIndex]--;
                else
                    Logs.EstimateFee.LogInformation(
                        $"Blockpolicy error, mempool tx removed from >25 blocks,bucketIndex={bucketIndex} already");
            }
            else
            {
                var blockIndex = entryHeight % this.unconfTxs.Count;
                if (this.unconfTxs[blockIndex][bucketIndex] > 0)
                    this.unconfTxs[blockIndex][bucketIndex]--;
                else
                    Logs.EstimateFee.LogInformation(
                        $"Blockpolicy error, mempool tx removed from blockIndex={blockIndex},bucketIndex={bucketIndex} already");
            }
        }

        // Update our estimates by decaying our historical moving average and updating
        //	with the data gathered from the current block 

        public void UpdateMovingAverages()
        {
            for (var j = 0; j < this.buckets.Count; j++)
            {
                for (var i = 0; i < this.confAvg.Count; i++)
                    this.confAvg[i][j] = this.confAvg[i][j] * this.decay + this.curBlockConf[i][j];
                this.avg[j] = this.avg[j] * this.decay + this.curBlockVal[j];
                this.txCtAvg[j] = this.txCtAvg[j] * this.decay + this.curBlockTxCt[j];
            }
        }

        // Calculate a feerate estimate.  Find the lowest value bucket (or range of buckets
        // to make sure we have enough data points) whose transactions still have sufficient likelihood
        // of being confirmed within the target number of confirmations
        // @param confTarget target number of confirmations
        // @param sufficientTxVal required average number of transactions per block in a bucket range
        // @param minSuccess the success probability we require
        // @param requireGreater return the lowest feerate such that all higher values pass minSuccess OR
        //        return the highest feerate such that all lower values fail minSuccess
        // @param nBlockHeight the current block height
        public double EstimateMedianVal(int confTarget, double sufficientTxVal, double successBreakPoint,
            bool requireGreater,
            int nBlockHeight)
        {
            // Counters for a bucket (or range of buckets)
            double nConf = 0; // Number of tx's confirmed within the confTarget
            double totalNum = 0; // Total number of tx's that were ever confirmed
            var extraNum = 0; // Number of tx's still in mempool for confTarget or longer

            var maxbucketindex = this.buckets.Count - 1;

            // requireGreater means we are looking for the lowest feerate such that all higher
            // values pass, so we start at maxbucketindex (highest feerate) and look at successively
            // smaller buckets until we reach failure.  Otherwise, we are looking for the highest
            // feerate such that all lower values fail, and we go in the opposite direction.
            var startbucket = requireGreater ? maxbucketindex : 0;
            var step = requireGreater ? -1 : 1;

            // We'll combine buckets until we have enough samples.
            // The near and far variables will define the range we've combined
            // The best variables are the last range we saw which still had a high
            // enough confirmation rate to count as success.
            // The cur variables are the current range we're counting.
            var curNearBucket = startbucket;
            var bestNearBucket = startbucket;
            var curFarBucket = startbucket;
            var bestFarBucket = startbucket;

            var foundAnswer = false;
            var bins = this.unconfTxs.Count;

            // Start counting from highest(default) or lowest feerate transactions
            for (var bucket = startbucket; bucket >= 0 && bucket <= maxbucketindex; bucket += step)
            {
                curFarBucket = bucket;
                nConf += this.confAvg[confTarget - 1][bucket];
                totalNum += this.txCtAvg[bucket];
                for (var confct = confTarget; confct < this.GetMaxConfirms(); confct++)
                    extraNum += this.unconfTxs[(nBlockHeight - confct) % bins][bucket];
                extraNum += this.oldUnconfTxs[bucket];
                // If we have enough transaction data points in this range of buckets,
                // we can test for success
                // (Only count the confirmed data points, so that each confirmation count
                // will be looking at the same amount of data and same bucket breaks)
                if (totalNum >= sufficientTxVal / (1 - this.decay))
                {
                    var curPct = nConf / (totalNum + extraNum);

                    // Check to see if we are no longer getting confirmed at the success rate
                    if (requireGreater && curPct < successBreakPoint)
                        break;
                    if (!requireGreater && curPct > successBreakPoint)
                        break;

                    // Otherwise update the cumulative stats, and the bucket variables
                    // and reset the counters
                    foundAnswer = true;
                    nConf = 0;
                    totalNum = 0;
                    extraNum = 0;
                    bestNearBucket = curNearBucket;
                    bestFarBucket = curFarBucket;
                    curNearBucket = bucket + step;
                }
            }

            double median = -1;
            double txSum = 0;

            // Calculate the "average" feerate of the best bucket range that met success conditions
            // Find the bucket with the median transaction and then report the average feerate from that bucket
            // This is a compromise between finding the median which we can't since we don't save all tx's
            // and reporting the average which is less accurate
            var minBucket = bestNearBucket < bestFarBucket ? bestNearBucket : bestFarBucket;
            var maxBucket = bestNearBucket > bestFarBucket ? bestNearBucket : bestFarBucket;
            for (var j = minBucket; j <= maxBucket; j++)
                txSum += this.txCtAvg[j];
            if (foundAnswer && txSum != 0)
            {
                txSum = txSum / 2;
                for (var j = minBucket; j <= maxBucket; j++)
                    if (this.txCtAvg[j] < txSum)
                    {
                        txSum -= this.txCtAvg[j];
                    }
                    else
                    {
                        // we're in the right bucket
                        median = this.avg[j] / this.txCtAvg[j];
                        break;
                    }
            }

            Logs.EstimateFee.LogInformation(
                $"{confTarget}: For conf success {(requireGreater ? $">" : $"<")} {successBreakPoint} need feerate {(requireGreater ? $">" : $"<")}: {median} from buckets {this.buckets[minBucket]} -{this.buckets[maxBucket]}  Cur Bucket stats {100 * nConf / (totalNum + extraNum)}  {nConf}/({totalNum}+{extraNum} mempool)");

            return median;
        }

        // Return the max number of confirms we're tracking 
        public int GetMaxConfirms()
        {
            return this.confAvg.Count;
        }

        // Write state of estimation data to a file
        public void Write(BitcoinStream stream)
        {
        }

        // Read saved state of estimation data from a file and replace all internal data structures and
        // variables with this state.
        public void Read(Stream filein)
        {
        }
    }
}