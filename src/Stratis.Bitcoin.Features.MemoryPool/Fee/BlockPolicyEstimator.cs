using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    /// <summary>
    /// The BlockPolicyEstimator is used for estimating the feerate needed
    /// for a transaction to be included in a block within a certain number of
    /// blocks.
    /// </summary>
    /// <remarks>
    /// At a high level the algorithm works by grouping transactions into buckets
    /// based on having similar feerates and then tracking how long it
    /// takes transactions in the various buckets to be mined.  It operates under
    /// the assumption that in general transactions of higher feerate will be
    /// included in blocks before transactions of lower feerate.   So for
    /// example if you wanted to know what feerate you should put on a transaction to
    /// be included in a block within the next 5 blocks, you would start by looking
    /// at the bucket with the highest feerate transactions and verifying that a
    /// sufficiently high percentage of them were confirmed within 5 blocks and
    /// then you would look at the next highest feerate bucket, and so on, stopping at
    /// the last bucket to pass the test.The average feerate of transactions in this
    /// bucket will give you an indication of the lowest feerate you can put on a
    /// transaction and still have a sufficiently high chance of being confirmed
    /// within your desired 5 blocks.
    ///
    /// Here is a brief description of the implementation:
    /// When a transaction enters the mempool, we track the height of the block chain
    /// at entry.  All further calculations are conducted only on this set of "seen"
    /// transactions.Whenever a block comes in, we count the number of transactions
    /// in each bucket and the total amount of feerate paid in each bucket. Then we
    /// calculate how many blocks Y it took each transaction to be mined.  We convert
    /// from a number of blocks to a number of periods Y' each encompassing "scale"
    /// blocks.This is tracked in 3 different data sets each up to a maximum
    /// number of periods.Within each data set we have an array of counters in each
    /// feerate bucket and we increment all the counters from Y' up to max periods
    /// representing that a tx was successfully confirmed in less than or equal to
    /// that many periods. We want to save a history of this information, so at any
    /// time we have a counter of the total number of transactions that happened in a
    /// given feerate bucket and the total number that were confirmed in each of the
    /// periods or less for any bucket.  We save this history by keeping an
    /// exponentially decaying moving average of each one of these stats.  This is
    /// done for a different decay in each of the 3 data sets to keep relevant data
    /// from different time horizons.  Furthermore we also keep track of the number
    /// unmined (in mempool or left mempool without being included in a block)
    /// transactions in each bucket and for how many blocks they have been
    /// outstanding and use both of these numbers to increase the number of transactions
    /// we've seen in that feerate bucket when calculating an estimate for any number
    /// of confirmations below the number of blocks they've been outstanding.
    /// </remarks>
    public class BlockPolicyEstimator
    {
        ///<summary>Track confirm delays up to 12 blocks for short horizon</summary>
        private const int ShortBlockPeriods = 12;
        private const int ShortScale = 1;
        
        ///<summary>Track confirm delays up to 48 blocks for medium horizon</summary>
        private const int MedBlockPeriods = 24;
        private const int MedScale = 2;

        ///<summary>Track confirm delays up to 1008 blocks for long horizon</summary>
        private const int LongBlockPeriods = 42;
        private const int LongScale = 24;

        ///<summary>Historical estimates that are older than this aren't valid</summary>
        private const int OldestEstimateHistory = 6 * 1008;

        ///<summary>Decay of .962 is a half-life of 18 blocks or about 3 hours</summary>
        private const double ShortDecay = .962;

        ///<summary>Decay of .998 is a half-life of 144 blocks or about 1 day</summary>
        private const double MedDecay = .9952;

        ///<summary>Decay of .9995 is a half-life of 1008 blocks or about 1 week</summary>
        private const double LongDecay = .99931;

        ///<summary>Require greater than 60% of X feerate transactions to be confirmed within Y/2 blocks</summary>
        private const double HalfSuccessPct = .6;

        ///<summary>Require greater than 85% of X feerate transactions to be confirmed within Y blocks</summary>
        private const double SuccessPct = .85;

        ///<summary>Require greater than 95% of X feerate transactions to be confirmed within 2 * Y blocks</summary>
        private const double DoubleSuccessPct = .95;

        /// <summary>Require an avg of 0.1 tx in the combined feerate bucket per block to have stat significance.</summary>
        private const double SufficientFeeTxs = 0.1;

        /// <summary>Require an avg of 0.5 tx when using short decay since there are fewer blocks considered</summary>
        private const double SufficientTxsShort = 0.5;

        /// <summary>Minimum and Maximum values for tracking feerates
        /// The MinBucketFeeRate should just be set to the lowest reasonable feerate we
        /// might ever want to track.  Historically this has been 1000 since it was
        /// inheriting DEFAULT_MIN_RELAY_TX_FEE and changing it is disruptive as it
        /// invalidates old estimates files. So leave it at 1000 unless it becomes
        /// necessary to lower it, and then lower it substantially.</summary>
        private const double MinBucketFeeRate = 1000;
        private const double MaxBucketFeeRate = 1e7;

        /// <summary>
        /// Spacing of FeeRate buckets.
        /// </summary>
        /// <remarks>
        /// We have to lump transactions into buckets based on feerate, but we want to be able
        /// to give accurate estimates over a large range of potential feerates.
        /// Therefore it makes sense to exponentially space the buckets.
        /// </remarks>
        private const double FeeSpacing = 1.05;

        private const double InfFeeRate = 1e99;

        /// <summary>Best seen block height.</summary>
        private int nBestSeenHeight;

        private int firstRecordedHeight;
        private int historicalFirst;
        private int historicalBest;

        /// <summary>Logger for logging on this object.</summary>
        private readonly ILogger logger;

        /// <summary>Classes to track historical data on transaction confirmations.</summary>
        private readonly TxConfirmStats feeStats;
        private readonly TxConfirmStats shortStats;
        private readonly TxConfirmStats longStats;

        /// <summary>Map of txids to information about that transaction.</summary>
        private readonly Dictionary<uint256, TxStatsInfo> mapMemPoolTxs;

        /// <summary>Setting for the node.</summary>
        private readonly MempoolSettings mempoolSettings;

        /// <summary>Count of tracked transactions.</summary>
        private int trackedTxs;

        /// <summary>Count of untracked transactions.</summary>
        private int untrackedTxs;

        /// <summary>
        /// The upper-bound of the range for the bucket (inclusive).
        /// </summary>
        /// <remarks>
        /// Define the buckets we will group transactions into.
        /// </remarks>
        private List<double> buckets;

        /// <summary>Map of bucket upper-bound to index into all vectors by bucket.</summary>
        private SortedDictionary<double, int> bucketMap;

        private object lockObject;

        /// <summary>
        /// Constructs an instance of the block policy estimator object.
        /// </summary>
        /// <param name="mempoolSettings">Mempool settings.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="nodeSettings">Full node settings.</param>
        public BlockPolicyEstimator(MempoolSettings mempoolSettings, ILoggerFactory loggerFactory, NodeSettings nodeSettings)
        {
            Guard.Assert(MinBucketFeeRate > 0);
            this.lockObject = new object();
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.mempoolSettings = mempoolSettings;
            this.mapMemPoolTxs = new Dictionary<uint256, TxStatsInfo>();
            this.buckets = new List<double>();
            this.bucketMap = new SortedDictionary<double, int>();
            this.nBestSeenHeight = 0;
            this.firstRecordedHeight = 0;
            this.historicalFirst = 0;
            this.historicalBest = 0;
            this.trackedTxs = 0;
            this.untrackedTxs = 0;
            int bucketIndex = 0;
            for(double bucketBoundary = MinBucketFeeRate; bucketBoundary <= MaxBucketFeeRate; bucketBoundary *= FeeSpacing, bucketIndex++)
            {
                this.buckets.Add(bucketBoundary);
                this.bucketMap.Add(bucketBoundary, bucketIndex);
            }
            this.buckets.Add(InfFeeRate);
            this.bucketMap.Add(InfFeeRate, bucketIndex);
            Guard.Assert(this.bucketMap.Count == this.buckets.Count);

            this.feeStats = new TxConfirmStats(this.logger);
            this.feeStats.Initialize(this.buckets, this.bucketMap, MedBlockPeriods, MedDecay, MedScale);
            this.shortStats = new TxConfirmStats(this.logger);
            this.shortStats.Initialize(this.buckets, this.bucketMap, ShortBlockPeriods, ShortDecay, ShortScale);
            this.longStats = new TxConfirmStats(this.logger);
            this.longStats.Initialize(this.buckets, this.bucketMap, LongBlockPeriods, LongDecay, LongScale);
        }

        /// <summary>
        /// Process all the transactions that have been included in a block.
        /// </summary>
        /// <param name="nBlockHeight">The block height for the block.</param>
        /// <param name="entries">Collection of memory pool entries.</param>
        public void ProcessBlock(int nBlockHeight, List<TxMempoolEntry> entries)
        {
            lock (this.lockObject)
            {
                if (nBlockHeight <= this.nBestSeenHeight)
                    // Ignore side chains and re-orgs; assuming they are random
                    // they don't affect the estimate.
                    // And if an attacker can re-org the chain at will, then
                    // you've got much bigger problems than "attacker can influence
                    // transaction fees."
                    return;

                // Must update nBestSeenHeight in sync with ClearCurrent so that
                // calls to removeTx (via processBlockTx) correctly calculate age
                // of unconfirmed txs to remove from tracking.
                this.nBestSeenHeight = nBlockHeight;

                // Update unconfirmed circular buffer
                this.feeStats.ClearCurrent(nBlockHeight);
                this.shortStats.ClearCurrent(nBlockHeight);
                this.longStats.ClearCurrent(nBlockHeight);

                // Decay all exponential averages
                this.feeStats.UpdateMovingAverages();
                this.shortStats.UpdateMovingAverages();
                this.longStats.UpdateMovingAverages();

                int countedTxs = 0;
                // Repopulate the current block states
                foreach (var entry in entries)
                {
                    if (this.ProcessBlockTx(nBlockHeight, entry))
                        countedTxs++;
                }
                
                if (this.firstRecordedHeight == 0 && countedTxs > 0)
                {
                    this.firstRecordedHeight = this.nBestSeenHeight;
                    this.logger.LogInformation("Blockpolicy first recorded height {0}", this.firstRecordedHeight);
                }

                // TODO: this makes too  much noise right now, put it back when logging is can be switched on by categories (and also consider disabling during IBD)
                // Logging.Logs.EstimateFee.LogInformation(
                // $"Blockpolicy after updating estimates for {countedTxs} of {entries.Count} txs in block, since last block {trackedTxs} of {trackedTxs + untrackedTxs} tracked, new mempool map size {mapMemPoolTxs.Count}");

                this.trackedTxs = 0;
                this.untrackedTxs = 0;
            }
        }

        /// <summary>
        /// Process a transaction confirmed in a block.
        /// </summary>
        /// <param name="nBlockHeight">Height of the block.</param>
        /// <param name="entry">The memory pool entry.</param>
        /// <returns>Whether it was able to successfully process the transaction.</returns>
        private bool ProcessBlockTx(int nBlockHeight, TxMempoolEntry entry)
        {
            if (!this.RemoveTx(entry.TransactionHash, true))
                return false;

            // How many blocks did it take for miners to include this transaction?
            // blocksToConfirm is 1-based, so a transaction included in the earliest
            // possible block has confirmation count of 1
            int blocksToConfirm = nBlockHeight - entry.EntryHeight;
            if (blocksToConfirm <= 0)
            {
                // This can't happen because we don't process transactions from a block with a height
                // lower than our greatest seen height
                this.logger.LogInformation($"Blockpolicy error Transaction had negative blocksToConfirm");
                return false;
            }

            // Feerates are stored and reported as BTC-per-kb:
            FeeRate feeRate = new FeeRate(entry.Fee, (int)entry.GetTxSize());

            this.feeStats.Record(blocksToConfirm, feeRate.FeePerK.Satoshi);
            this.shortStats.Record(blocksToConfirm, feeRate.FeePerK.Satoshi);
            this.longStats.Record(blocksToConfirm, feeRate.FeePerK.Satoshi);
            return true;
        }

        /// <summary>
        ///  Process a transaction accepted to the mempool.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="validFeeEstimate">Whether to update fee estimate.</param>
        public void ProcessTransaction(TxMempoolEntry entry, bool validFeeEstimate)
        {
            lock (this.lockObject)
            {
                int txHeight = entry.EntryHeight;
                uint256 hash = entry.TransactionHash;
                if (this.mapMemPoolTxs.ContainsKey(hash))
                {
                    this.logger.LogInformation($"Blockpolicy error mempool tx {hash} already being tracked");
                    return;
                }

                if (txHeight != this.nBestSeenHeight)
                    return;

                // Only want to be updating estimates when our blockchain is synced,
                // otherwise we'll miscalculate how many blocks its taking to get included.
                if (!validFeeEstimate)
                {
                    this.untrackedTxs++;
                    return;
                }
                this.trackedTxs++;

                // Feerates are stored and reported as BTC-per-kb:
                FeeRate feeRate = new FeeRate(entry.Fee, (int)entry.GetTxSize());

                this.mapMemPoolTxs.Add(hash, new TxStatsInfo());
                this.mapMemPoolTxs[hash].blockHeight = txHeight;
                int bucketIndex = this.feeStats.NewTx(txHeight, feeRate.FeePerK.Satoshi);
                this.mapMemPoolTxs[hash].bucketIndex = bucketIndex;
                int bucketIndex2 = this.shortStats.NewTx(txHeight, feeRate.FeePerK.Satoshi);
                Guard.Assert(bucketIndex == bucketIndex2);
                int bucketIndex3 = this.longStats.NewTx(txHeight, feeRate.FeePerK.Satoshi);
                Guard.Assert(bucketIndex == bucketIndex3);
            }
        }

        /// <summary>
        /// Remove a transaction from the mempool tracking stats.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        /// <returns>Whether the transaction was successfully removed.</returns>
        /// <remarks>
        /// This function is called from TxMemPool.RemoveUnchecked to ensure
        /// txs removed from the mempool for any reason are no longer
        /// tracked. Txs that were part of a block have already been removed in
        /// ProcessBlockTx to ensure they are never double tracked, but it is
        /// of no harm to try to remove them again.
        /// </remarks>
        public bool RemoveTx(uint256 hash, bool inBlock)
        {
            lock (this.lockObject)
            {
                TxStatsInfo pos = this.mapMemPoolTxs.TryGet(hash);
                if (pos != null)
                {
                    this.feeStats.RemoveTx(pos.blockHeight, this.nBestSeenHeight, pos.bucketIndex, inBlock);
                    this.shortStats.RemoveTx(pos.blockHeight, this.nBestSeenHeight, pos.bucketIndex, inBlock);
                    this.longStats.RemoveTx(pos.blockHeight, this.nBestSeenHeight, pos.bucketIndex, inBlock);
                    this.mapMemPoolTxs.Remove(hash);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Return an estimate fee according to horizon
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block</param>
        public FeeRate EstimateRawFee(int confTarget, double successThreshold, FeeEstimateHorizon horizon, EstimationResult result)
        {
            TxConfirmStats stats;
            double sufficientTxs = SufficientFeeTxs;
            switch (horizon)
            {
                case FeeEstimateHorizon.ShortHalfLife:
                {
                    stats = this.shortStats;
                    sufficientTxs = SufficientTxsShort;
                    break;
                }
                case FeeEstimateHorizon.MedHalfLife:
                {
                    stats = this.feeStats;
                    break;
                }
                case FeeEstimateHorizon.LongHalfLife:
                {
                    stats = this.longStats;
                    break;
                }
                default:
                {
                        throw new ArgumentException(nameof(horizon));
                }
            }
            lock(this.lockObject)
            {
                // Return failure if trying to analyze a target we're not tracking
                if (confTarget <= 0 || confTarget > stats.GetMaxConfirms())
                {
                    return new FeeRate(0);
                }
                if (successThreshold > 1)
                {
                    return new FeeRate(0);      
                }

                double median = stats.EstimateMedianVal(confTarget, sufficientTxs, successThreshold,
                    true, this.nBestSeenHeight, result);

                if (median < 0)
                    return new FeeRate(0);

                return new FeeRate(Convert.ToInt64(median));
            }
        }

        /// <summary>
        /// Return a feerate estimate
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block.</param>
        public FeeRate EstimateFee(int confTarget)
        {
            // It's not possible to get reasonable estimates for confTarget of 1
            if (confTarget <= 1)
            {
                return new FeeRate(0);
            }

            return EstimateRawFee(confTarget, DoubleSuccessPct, FeeEstimateHorizon.MedHalfLife, null);
        }

        public int HighestTargetTracked(FeeEstimateHorizon horizon)
        {
            switch (horizon)
            {
                case FeeEstimateHorizon.ShortHalfLife:
                {
                    return this.shortStats.GetMaxConfirms();
                }
                case FeeEstimateHorizon.MedHalfLife:
                {
                    return this.feeStats.GetMaxConfirms();
                }
                case FeeEstimateHorizon.LongHalfLife:
                {
                    return this.longStats.GetMaxConfirms();
                }
                default:
                {
                    throw new ArgumentException(nameof(horizon));
                }
            }
        }

        private int BlockSpan()
        {
            if (this.firstRecordedHeight == 0) return 0;
            Guard.Assert(this.nBestSeenHeight >= this.firstRecordedHeight);

            return this.nBestSeenHeight - this.firstRecordedHeight;
        }

        private int HistoricalBlockSpan()
        {
            if (this.historicalFirst == 0) return 0;
            Guard.Assert(this.historicalBest >= this.historicalFirst);

            if (this.nBestSeenHeight - this.historicalBest > OldestEstimateHistory)
            {
                return 0;
            }

            return this.historicalBest - this.historicalFirst;
        }

        private int MaxUsableEstimate()
        {
            // Block spans are divided by 2 to make sure there are enough potential failing data points for the estimate
            return Math.Min(this.longStats.GetMaxConfirms(), Math.Max(BlockSpan(), HistoricalBlockSpan()) / 2);
        }

        /// <summary>
        /// Return a fee estimate at the required successThreshold from the shortest
        /// time horizon which tracks confirmations up to the desired target.If
        /// checkShorterHorizon is requested, also allow short time horizon estimates
        /// for a lower target to reduce the given answer
        /// </summary>
        private double EstimateCombinedFee(int confTarget, double successThreshold, 
            bool checkShorterHorizon, EstimationResult result)
        {
            double estimate = -1;
            if (confTarget >= 1 && confTarget <= this.longStats.GetMaxConfirms())
            {
                // Find estimate from shortest time horizon possible
                if (confTarget <= this.shortStats.GetMaxConfirms())
                { // short horizon
                    estimate = this.shortStats.EstimateMedianVal(confTarget, SufficientTxsShort, 
                        successThreshold, true, this.nBestSeenHeight, result);
                }
                else if (confTarget <= this.feeStats.GetMaxConfirms())
                { // medium horizon
                    estimate = this.feeStats.EstimateMedianVal(confTarget, SufficientFeeTxs, 
                        successThreshold, true, this.nBestSeenHeight, result);
                }
                else
                { // long horizon
                    estimate = this.longStats.EstimateMedianVal(confTarget, SufficientFeeTxs, 
                        successThreshold, true, this.nBestSeenHeight, result);
                }
                if (checkShorterHorizon)
                {
                    EstimationResult tempResult = new EstimationResult();
                    // If a lower confTarget from a more recent horizon returns a lower answer use it.
                    if (confTarget > this.feeStats.GetMaxConfirms())
                    {
                        double medMax = this.feeStats.EstimateMedianVal(this.feeStats.GetMaxConfirms(), 
                            SufficientFeeTxs, successThreshold, true, this.nBestSeenHeight, tempResult);
                        if (medMax > 0 && (estimate == -1 || medMax < estimate))
                        {
                            estimate = medMax;
                            if (result != null)
                            {
                                result = tempResult;
                            }
                        }
                    }
                    if (confTarget > this.shortStats.GetMaxConfirms())
                    {
                        double shortMax = this.shortStats.EstimateMedianVal(this.shortStats.GetMaxConfirms(), 
                            SufficientTxsShort, successThreshold, true, this.nBestSeenHeight, tempResult);
                        if (shortMax > 0 && (estimate == -1 || shortMax < estimate))
                        {
                            estimate = shortMax;
                            if (result != null)
                            {
                                result = tempResult;
                            }
                        }
                    }
                }
            }
            return estimate;
        }

        /// <summary>
        /// Ensure that for a conservative estimate, the DOUBLE_SUCCESS_PCT is also met
        /// at 2 * target for any longer time horizons.
        /// </summary>
        private double EstimateConservativeFee(int doubleTarget, EstimationResult result)
        {
            double estimate = -1;
            EstimationResult tempResult = new EstimationResult();
            if (doubleTarget <= this.shortStats.GetMaxConfirms())
            {
                estimate = this.feeStats.EstimateMedianVal(doubleTarget, SufficientFeeTxs, 
                    DoubleSuccessPct, true, this.nBestSeenHeight, result);
            }
            if (doubleTarget <= this.feeStats.GetMaxConfirms())
            {
                double longEstimate = this.longStats.EstimateMedianVal(doubleTarget, SufficientFeeTxs, 
                    DoubleSuccessPct, true, this.nBestSeenHeight, tempResult);
                if (longEstimate > estimate)
                {
                    estimate = longEstimate;
                    if (result != null)
                    {
                        result = tempResult;
                    }
                }
            }
            return estimate;
        }

        /// <summary>
        /// Returns the max of the feerates calculated with a 60%
        /// threshold required at target / 2, an 85% threshold required at target and a
        /// 95% threshold required at 2 * target.Each calculation is performed at the
        /// shortest time horizon which tracks the required target.Conservative
        /// estimates, however, required the 95% threshold at 2 * target be met for any
        /// longer time horizons also.
        /// </summary>
        public FeeRate EstimateSmartFee(int confTarget, FeeCalculation feeCalc, bool conservative)
        {
            lock (this.lockObject)
            {

                if (feeCalc != null)
                {
                    feeCalc.DesiredTarget = confTarget;
                    feeCalc.ReturnedTarget = confTarget;
                }

                double median = -1;
                EstimationResult tempResult = new EstimationResult();

                // Return failure if trying to analyze a target we're not tracking
                if (confTarget <= 0 || confTarget > this.longStats.GetMaxConfirms())
                {
                    return new FeeRate(0);  // error condition
                }

                // It's not possible to get reasonable estimates for confTarget of 1
                if (confTarget == 1)
                {
                    confTarget = 2;
                }

                int maxUsableEstimate = MaxUsableEstimate();
                if (confTarget > maxUsableEstimate)
                {
                    confTarget = maxUsableEstimate;
                }
                if (feeCalc != null)
                {
                    feeCalc.ReturnedTarget = confTarget;
                }

                if (confTarget <= 1)
                {
                    return new FeeRate(0); // error condition
                }

                Guard.Assert(confTarget > 0); //estimateCombinedFee and estimateConservativeFee take unsigned ints

                // true is passed to estimateCombined fee for target/2 and target so
                // that we check the max confirms for shorter time horizons as well.
                // This is necessary to preserve monotonically increasing estimates.
                // For non-conservative estimates we do the same thing for 2*target, but
                // for conservative estimates we want to skip these shorter horizons
                // checks for 2*target because we are taking the max over all time
                // horizons so we already have monotonically increasing estimates and
                // the purpose of conservative estimates is not to let short term
                // fluctuations lower our estimates by too much.
                double halfEst = EstimateCombinedFee(confTarget / 2, HalfSuccessPct, true, tempResult);
                if (feeCalc != null)
                {
                    feeCalc.Estimation = tempResult;
                    feeCalc.Reason = FeeReason.HalfEstimate;
                }
                median = halfEst;
                double actualEst = EstimateCombinedFee(confTarget, SuccessPct, true, tempResult);
                if (actualEst > median)
                {
                    median = actualEst;
                    if (feeCalc != null)
                    {
                        feeCalc.Estimation = tempResult;
                        feeCalc.Reason = FeeReason.FullEstimate;
                    }
                }
                double doubleEst = EstimateCombinedFee(2 * confTarget, DoubleSuccessPct, !conservative, tempResult);
                if (doubleEst > median)
                {
                    median = doubleEst;
                    if (feeCalc != null)
                    {
                        feeCalc.Estimation = tempResult;
                        feeCalc.Reason = FeeReason.DoubleEstimate;
                    }
                }

                if (conservative || median == -1)
                {
                    double consEst = EstimateConservativeFee(2 * confTarget, tempResult);
                    if (consEst > median)
                    {
                        median = consEst;
                        if (feeCalc != null)
                        {
                            feeCalc.Estimation = tempResult;
                            feeCalc.Reason = FeeReason.Coservative;
                        }
                    }
                }

                if (median < 0) return new FeeRate(0); // error condition

                return new FeeRate(Convert.ToInt64(median));
            }
        }
        /// <summary>
        /// Write estimation data to a file.
        /// </summary>
        /// <param name="fileout">Stream to write to.</param>
        /// <remarks>TODO: Implement write estimation</remarks>
        public void Write(Stream fileout)
        {
        }

        /// <summary>
        /// Read estimation data from a file.
        /// </summary>
        /// <param name="filein">Stream to read data from.</param>
        /// <param name="nFileVersion">Version number of the file.</param>
        /// <remarks>TODO: Implement read estimation</remarks>
        public void Read(Stream filein, int nFileVersion)
        {
        }

        public void FlushUncomfirmed()
        {
            lock (this.lockObject)
            {
                int numEntries = this.mapMemPoolTxs.Count;
                // Remove every entry in mapMemPoolTxs
                while (this.mapMemPoolTxs.Count > 0)
                {
                    var mi = this.mapMemPoolTxs.First(); ;
                    RemoveTx(mi.Key, false); // this calls erase() on mapMemPoolTxs
                }
                this.logger.LogInformation($"Recorded {numEntries} unconfirmed txs from mempool");
            }
        }
                
        /// <summary>
        /// Transaction statistics information.
        /// </summary>
        public class TxStatsInfo
        {
            /// <summary>The block height.</summary>
            public int blockHeight;

            /// <summary>The index into the confirmed transactions bucket map.</summary>
            public int bucketIndex;

            /// <summary>
            /// Constructs a instance of a transaction stats info object.
            /// </summary>
            public TxStatsInfo()
            {
                this.blockHeight = 0;
                this.bucketIndex = 0;
            }
        }
    }
}
