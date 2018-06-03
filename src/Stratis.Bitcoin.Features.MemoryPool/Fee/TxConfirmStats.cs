using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// Transation confirmation statistics.
    /// </summary>
    public class TxConfirmStats
    {
        /// <summary>Instance logger for logging messages.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// The upper-bound of the range for the bucket (inclusive).
        /// </summary>
        /// <remarks>
        /// Define the buckets we will group transactions into.
        /// </remarks>
        private List<double> buckets;

        /// <summary>Map of bucket upper-bound to index into all vectors by bucket.</summary>
        private SortedDictionary<double, int> bucketMap;

        /// <summary>
        /// Historical moving average of transaction counts.
        /// </summary>
        /// <remarks>
        /// For each bucket X:
        /// Count the total # of txs in each bucket.
        /// Track the historical moving average of this total over blocks
        /// </remarks>
        private List<double> txCtAvg;

        /// <summary>
        /// Confirmation average. confAvg[Y][X]
        /// </summary>
        /// <remarks>
        /// Count the total # of txs confirmed within Y blocks in each bucket.
        /// Track the historical moving average of theses totals over blocks.
        /// </remarks>
        private List<List<double>> confAvg;

        /// <summary>
        /// Failed average. failAvg[Y][X]
        /// </summary>
        /// <remarks>
        /// Track moving avg of txs which have been evicted from the mempool
        /// after failing to be confirmed within Y blocks
        /// </remarks>
        private List<List<double>> failAvg;

        /// <summary>
        /// Moving average of total fee rate of all transactions in each bucket.
        /// </summary>
        /// <remarks>
        /// Track the historical moving average of this total over blocks.
        /// </remarks>
        private List<double> avg;

        // Combine the conf counts with tx counts to calculate the confirmation % for each Y,X
        // Combine the total value with the tx counts to calculate the avg feerate per bucket

        /// <summary>Decay value to use.</summary>
        private double decay;

        /// <summary>
        /// Resolution (# of blocks) with which confirmations are tracked
        /// </summary>
        private int scale;

        /// <summary>
        /// Mempool counts of outstanding transactions.
        /// </summary>
        /// <remarks>
        /// For each bucket X, track the number of transactions in the mempool
        /// that are unconfirmed for each possible confirmation value Y
        /// unconfTxs[Y][X]
        /// </remarks>
        private List<List<int>> unconfTxs;

        /// <summary>Transactions still unconfirmed after MAX_CONFIRMS for each bucket</summary>
        private List<int> oldUnconfTxs;


        /// <summary>
        /// Constructs an instance of the transaction confirmation stats object.
        /// </summary>
        /// <param name="logger">Instance logger to use for message logging.</param>
        public TxConfirmStats(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Initialize the data structures.  This is called by BlockPolicyEstimator's
        /// constructor with default values.
        /// </summary>
        /// <param name="defaultBuckets">Contains the upper limits for the bucket boundaries.</param>
        /// <param name="maxPeriods">Max number of periods to track.</param>
        /// <param name="decay">How much to decay the historical moving average per block.</param>
        public void Initialize(List<double> defaultBuckets, IDictionary<double, int> defaultBucketMap,  int maxPeriods, double decay, int scale)
        {
            Guard.Assert(scale != 0);
            this.decay = decay;
            this.scale = scale;
            this.confAvg = new List<List<double>>();
            this.failAvg = new List<List<double>>();
            this.buckets = new List<double>(defaultBuckets);
            this.bucketMap = new SortedDictionary<double, int>(defaultBucketMap);
            this.unconfTxs = new List<List<int>>();

            for (int i = 0; i < maxPeriods; i++)
            {
                this.confAvg.Insert(i, Enumerable.Repeat(default(double), this.buckets.Count).ToList());
                this.failAvg.Insert(i, Enumerable.Repeat(default(double), this.buckets.Count).ToList());
            }


            this.txCtAvg = new List<double>(Enumerable.Repeat(default(double), this.buckets.Count));
            this.avg = new List<double>(Enumerable.Repeat(default(double), this.buckets.Count));
            ClearInMemoryCounters(this.buckets.Count);
        }

        private void ClearInMemoryCounters(int bucketsCount)
        {
            for (int i = 0; i < GetMaxConfirms(); i++)
            {
                this.unconfTxs.Insert(i, Enumerable.Repeat(default(int), bucketsCount).ToList());
            }
            this.oldUnconfTxs = new List<int>(Enumerable.Repeat(default(int), bucketsCount));
        }

        /// <summary>
        /// Clear the state of the curBlock variables to start counting for the new block.
        /// </summary>
        /// <param name="nBlockHeight">Block height.</param>
        public void ClearCurrent(int nBlockHeight)
        {
            for (var j = 0; j < this.buckets.Count; j++)
            {
                this.oldUnconfTxs[j] += this.unconfTxs[nBlockHeight % this.unconfTxs.Count][j];
                this.unconfTxs[nBlockHeight % this.unconfTxs.Count][j] = 0;
            }
        }

        /// <summary>
        /// Record a new transaction data point in the current block stats.
        /// </summary>
        /// <param name="blocksToConfirm">The number of blocks it took this transaction to confirm. blocksToConfirm is 1-based and has to be >= 1.</param>
        /// <param name="val">The feerate of the transaction.</param>
        public void Record(int blocksToConfirm, double val)
        {
            // blocksToConfirm is 1-based
            if (blocksToConfirm < 1)
                return;
            int periodsToConfirm = (blocksToConfirm + this.scale - 1) / this.scale;
            int bucketindex = this.bucketMap.FirstOrDefault(k => k.Key >= val).Value;
            for (int i = periodsToConfirm; i <= this.confAvg.Count; i++)
            {
                this.confAvg[i - 1][bucketindex]++;
            }
            this.txCtAvg[bucketindex]++;
            this.avg[bucketindex] += val;
        }

        /// <summary>
        /// Record a new transaction entering the mempool.
        /// </summary>
        /// <param name="nBlockHeight">The block height.</param>
        /// <param name="val"></param>
        /// <returns>The feerate of the transaction.</returns>
        public int NewTx(int nBlockHeight, double val)
        {
            int bucketindex = this.bucketMap.FirstOrDefault(k => k.Key >= val).Value;
            int blockIndex = nBlockHeight % this.unconfTxs.Count;
            this.unconfTxs[blockIndex][bucketindex]++;
            return bucketindex;
        }

        /// <summary>
        /// Remove a transaction from mempool tracking stats.
        /// </summary>
        /// <param name="entryHeight">The height of the mempool entry.</param>
        /// <param name="nBestSeenHeight">The best sceen height.</param>
        /// <param name="bucketIndex">The bucket index.</param>
        public void RemoveTx(int entryHeight, int nBestSeenHeight, int bucketIndex, bool inBlock)
        {
            //nBestSeenHeight is not updated yet for the new block
            int blocksAgo = nBestSeenHeight - entryHeight;
            if (nBestSeenHeight == 0) // the BlockPolicyEstimator hasn't seen any blocks yet
                blocksAgo = 0;
            if (blocksAgo < 0)
            {
                this.logger.LogInformation($"Blockpolicy error, blocks ago is negative for mempool tx");
                return; //This can't happen because we call this with our best seen height, no entries can have higher
            }

            if (blocksAgo >= this.unconfTxs.Count)
            {
                if (this.oldUnconfTxs[bucketIndex] > 0)
                    this.oldUnconfTxs[bucketIndex]--;
                else
                    this.logger.LogInformation(
                        $"Blockpolicy error, mempool tx removed from >25 blocks,bucketIndex={bucketIndex} already");
            }
            else
            {
                int blockIndex = entryHeight % this.unconfTxs.Count;
                if (this.unconfTxs[blockIndex][bucketIndex] > 0)
                    this.unconfTxs[blockIndex][bucketIndex]--;
                else
                    this.logger.LogInformation(
                        $"Blockpolicy error, mempool tx removed from blockIndex={blockIndex},bucketIndex={bucketIndex} already");
            }
            if(!inBlock && (blocksAgo >= this.scale)) // Only counts as a failure if not confirmed for entire period
            {
                Guard.Assert(this.scale != 0);
                int periodsAgo = blocksAgo / this.scale;
                for (int i = 0; i < periodsAgo && i < this.failAvg.Count; i++)
                {
                    this.failAvg[i][bucketIndex]++;
                }
            }
        }

        /// <summary>
        /// Update our estimates by decaying our historical moving average and updating
        /// with the data gathered from the current block.
        /// </summary>
        public void UpdateMovingAverages()
        {
            for (var j = 0; j < this.buckets.Count; j++)
            {
                for (var i = 0; i < this.confAvg.Count; i++)
                    this.confAvg[i][j] = this.confAvg[i][j] * this.decay;
                for (var i = 0; i < this.failAvg.Count; i++)
                    this.failAvg[i][j] = this.failAvg[i][j] * this.decay;
                this.avg[j] = this.avg[j] * this.decay;
                this.txCtAvg[j] = this.txCtAvg[j] * this.decay;
            }
        }

        /// <summary>
        /// Calculate a feerate estimate.  Find the lowest value bucket (or range of buckets
        /// to make sure we have enough data points) whose transactions still have sufficient likelihood
        /// of being confirmed within the target number of confirmations.
        /// </summary>
        /// <param name="confTarget">Target number of confirmations.</param>
        /// <param name="sufficientTxVal">Required average number of transactions per block in a bucket range.</param>
        /// <param name="successBreakPoint">The success probability we require.</param>
        /// <param name="requireGreater">Return the lowest feerate such that all higher values pass minSuccess OR return the highest feerate such that all lower values fail minSuccess.</param>
        /// <param name="nBlockHeight">The current block height.</param>
        /// <returns></returns>
        public double EstimateMedianVal(int confTarget, double sufficientTxVal, double successBreakPoint,
            bool requireGreater, int nBlockHeight, EstimationResult result)
        {
            // Counters for a bucket (or range of buckets)
            double nConf = 0; // Number of tx's confirmed within the confTarget
            double totalNum = 0; // Total number of tx's that were ever confirmed
            int extraNum = 0; // Number of tx's still in mempool for confTarget or longer
            double failNum = 0; // Number of tx's that were never confirmed but removed from the mempool after confTarget
            int periodTarget = (confTarget + this.scale - 1) / this.scale;

            int maxbucketindex = this.buckets.Count - 1;

            // requireGreater means we are looking for the lowest feerate such that all higher
            // values pass, so we start at maxbucketindex (highest feerate) and look at successively
            // smaller buckets until we reach failure.  Otherwise, we are looking for the highest
            // feerate such that all lower values fail, and we go in the opposite direction.
            int startbucket = requireGreater ? maxbucketindex : 0;
            int step = requireGreater ? -1 : 1;

            // We'll combine buckets until we have enough samples.
            // The near and far variables will define the range we've combined
            // The best variables are the last range we saw which still had a high
            // enough confirmation rate to count as success.
            // The cur variables are the current range we're counting.
            int curNearBucket = startbucket;
            int bestNearBucket = startbucket;
            int curFarBucket = startbucket;
            int bestFarBucket = startbucket;

            bool foundAnswer = false;
            int bins = this.unconfTxs.Count;
            bool newBucketRange = true;
            bool passing = true;
            EstimatorBucket passBucket = new EstimatorBucket(); ;
            EstimatorBucket failBucket = new EstimatorBucket();

            // Start counting from highest(default) or lowest feerate transactions
            for (int bucket = startbucket; bucket >= 0 && bucket <= maxbucketindex; bucket += step)
            {
                if (newBucketRange)
                {
                    curNearBucket = bucket;
                    newBucketRange = false;
                }
                curFarBucket = bucket;
                nConf += this.confAvg[periodTarget - 1][bucket];
                totalNum += this.txCtAvg[bucket];
                failNum += this.failAvg[periodTarget - 1][bucket];
                for (int confct = confTarget; confct < this.GetMaxConfirms(); confct++)
                    extraNum += this.unconfTxs[(nBlockHeight - confct) % bins][bucket];
                extraNum += this.oldUnconfTxs[bucket];
                // If we have enough transaction data points in this range of buckets,
                // we can test for success
                // (Only count the confirmed data points, so that each confirmation count
                // will be looking at the same amount of data and same bucket breaks)
                if (totalNum >= sufficientTxVal / (1 - this.decay))
                {
                    double curPct = nConf / (totalNum + failNum + extraNum);

                    // Check to see if we are no longer getting confirmed at the success rate
                    if ((requireGreater && curPct < successBreakPoint)||
                        (!requireGreater && curPct > successBreakPoint))
                    {
                        if (passing == true)
                        {
                            // First time we hit a failure record the failed bucket
                            int failMinBucket =  Math.Min(curNearBucket, curFarBucket);
                            int failMaxBucket = Math.Max(curNearBucket, curFarBucket);
                            failBucket.Start = failMinBucket > 0 ? this.buckets[failMinBucket - 1] : 0;
                            failBucket.End = this.buckets[failMaxBucket];
                            failBucket.WithinTarget = nConf;
                            failBucket.TotalConfirmed = totalNum;
                            failBucket.InMempool = extraNum;
                            failBucket.LeftMempool = failNum;
                            passing = false;
                        }
                        continue;
                    }

                    // Otherwise update the cumulative stats, and the bucket variables
                    // and reset the counters
                    else
                    {
                        failBucket = new EstimatorBucket(); // Reset any failed bucket, currently passing
                        foundAnswer = true;
                        passing = true;
                        passBucket.WithinTarget = nConf;
                        nConf = 0;
                        passBucket.TotalConfirmed = totalNum;
                        totalNum = 0;
                        passBucket.InMempool = extraNum;
                        passBucket.LeftMempool = failNum;
                        failNum = 0;
                        extraNum = 0;
                        bestNearBucket = curNearBucket;
                        bestFarBucket = curFarBucket;
                        newBucketRange = true;
                    }
                }
            }

            double median = -1;
            double txSum = 0;

            // Calculate the "average" feerate of the best bucket range that met success conditions
            // Find the bucket with the median transaction and then report the average feerate from that bucket
            // This is a compromise between finding the median which we can't since we don't save all tx's
            // and reporting the average which is less accurate
            int minBucket = Math.Min(bestNearBucket, bestFarBucket);
            int maxBucket = Math.Max(bestNearBucket, bestFarBucket);
            for (int j = minBucket; j <= maxBucket; j++)
            {
                txSum += this.txCtAvg[j];
            }
            if (foundAnswer && txSum != 0)
            {
                txSum = txSum / 2;
                for (int j = minBucket; j <= maxBucket; j++)
                {
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

                passBucket.Start = minBucket > 0 ? this.buckets[minBucket - 1] : 0;
                passBucket.End = this.buckets[maxBucket];
            }
            
            // If we were passing until we reached last few buckets with insufficient data, then report those as failed
            if (passing && !newBucketRange)
            {
                int failMinBucket = Math.Min(curNearBucket, curFarBucket);
                int failMaxBucket = Math.Max(curNearBucket, curFarBucket);
                failBucket.Start = failMinBucket > 0 ? this.buckets[failMinBucket - 1] : 0;
                failBucket.End = this.buckets[failMaxBucket];
                failBucket.WithinTarget = nConf;
                failBucket.TotalConfirmed = totalNum;
                failBucket.InMempool = extraNum;
                failBucket.LeftMempool = failNum;
            }

            this.logger.LogInformation(
                $"FeeEst: {confTarget} {(requireGreater ? $">" : $"<")} " +
                $"{successBreakPoint} decay {this.decay} feerate: {median}" +
                $" from ({passBucket.Start} - {passBucket.End}" +
                $" {100 * passBucket.WithinTarget / (passBucket.TotalConfirmed + passBucket.InMempool + passBucket.LeftMempool)}" +
                $" {passBucket.WithinTarget}/({passBucket.TotalConfirmed}" +
                $" {passBucket.InMempool} mem {passBucket.LeftMempool} out) " +
                $"Fail: ({failBucket.Start} - {failBucket.End} " +
                $"{100 * failBucket.WithinTarget / (failBucket.TotalConfirmed + failBucket.InMempool + failBucket.LeftMempool)}" +
                $" {failBucket.WithinTarget}/({failBucket.TotalConfirmed}" +
                $" {failBucket.InMempool} mem {failBucket.LeftMempool} out)");

            if (result != null)
            {
                result.Pass = passBucket;
                result.Fail = failBucket;
                result.Decay = this.decay;
                result.Scale = this.scale;
            }
            return median;
        }

        /// <summary>
        /// Return the max number of confirms we're tracking.
        /// </summary>
        /// <returns>The max number of confirms.</returns>
        public int GetMaxConfirms()
        {
            return this.scale * this.confAvg.Count;
        }

        /// <summary>
        /// Write state of estimation data to a file.
        /// </summary>
        /// <param name="stream">Stream to write to.</param>
        public void Write(BitcoinStream stream)
        {
        }

        /// <summary>
        /// Read saved state of estimation data from a file and replace all internal data structures and
        /// variables with this state.
        /// </summary>
        /// <param name="filein">Stream to read from.</param>
        public void Read(Stream filein)
        {
        }
    }
}
