using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

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
    /// the last bucket to pass the test.   The average feerate of transactions in this
    /// bucket will give you an indication of the lowest feerate you can put on a
    /// transaction and still have a sufficiently high chance of being confirmed
    /// within your desired 5 blocks.
    ///
    /// Here is a brief description of the implementation:
    /// When a transaction enters the mempool, we
    /// track the height of the block chain at entry.  Whenever a block comes in,
    /// we count the number of transactions in each bucket and the total amount of feerate
    /// paid in each bucket. Then we calculate how many blocks Y it took each
    /// transaction to be mined and we track an array of counters in each bucket
    /// for how long it to took transactions to get confirmed from 1 to a max of 25
    /// and we increment all the counters from Y up to 25. This is because for any
    /// number Z>=Y the transaction was successfully mined within Z blocks.  We
    /// want to save a history of this information, so at any time we have a
    /// counter of the total number of transactions that happened in a given feerate
    /// bucket and the total number that were confirmed in each number 1-25 blocks
    /// or less for any bucket.   We save this history by keeping an exponentially
    /// decaying moving average of each one of these stats.  Furthermore we also
    /// keep track of the number unmined (in mempool) transactions in each bucket
    /// and for how many blocks they have been outstanding and use that to increase
    /// the number of transactions we've seen in that feerate bucket when calculating
    /// an estimate for any number of confirmations below the number of blocks
    /// they've been outstanding.
    ///
    /// We will instantiate an instance of this class to track transactions that were
    /// included in a block. We will lump transactions into a bucket according to their
    /// approximate feerate and then track how long it took for those txs to be included in a block
    ///
    /// The tracking of unconfirmed (mempool) transactions is completely independent of the
    /// historical tracking of transactions that have been confirmed in a block.
    ///
    ///  We want to be able to estimate feerates that are needed on tx's to be included in
    /// a certain number of blocks.Every time a block is added to the best chain, this class records
    /// stats on the transactions included in that block
    /// </remarks>
    public class BlockPolicyEstimator
    {
        /// <summary>Require an avg of 1 tx in the combined feerate bucket per block to have stat significance.</summary>
        private const double SufficientFeeTxs = 1;

        /// <summary>Require greater than 95% of X feerate transactions to be confirmed within Y blocks for X to be big enough.</summary>
        private const double MinSuccessPct = .95;

        /// <summary>Minimum value for tracking feerates.</summary>
        private const long MinFeeRate = 10;

        /// <summary>Maximum value for tracking feerates.</summary>
        private const double MaxFeeRate = 1e7;

        /// <summary>
        /// Spacing of FeeRate buckets.
        /// </summary>
        /// <remarks>
        /// We have to lump transactions into buckets based on feerate, but we want to be able
        /// to give accurate estimates over a large range of potential feerates.
        /// Therefore it makes sense to exponentially space the buckets.
        /// </remarks>
        private const double FeeSpacing = 1.1;

        /// <summary>Track confirm delays up to 25 blocks, can't estimate beyond that.</summary>
        private const int MaxBlockConfirms = 25;

        /// <summary>Decay of .998 is a half-life of 346 blocks or about 2.4 days.</summary>
        private const double DefaultDecay = .998;

        /// <summary>Value for infinite priority.</summary>
        public const double InfPriority = 1e9 * 21000000ul * Money.COIN;

        /// <summary>Maximum money value.</summary>
        private static readonly Money MaxMoney = new Money(21000000 * Money.COIN);

        /// <summary>Value for infinite fee rate.</summary>
        private static readonly double InfFeeRate = MaxMoney.Satoshi;

        /// <summary>Classes to track historical data on transaction confirmations.</summary>
        private readonly TxConfirmStats feeStats;

        /// <summary>Map of txids to information about that transaction.</summary>
        private readonly Dictionary<uint256, TxStatsInfo> mapMemPoolTxs;

        /// <summary>Minimum tracked Fee. Passed to constructor to avoid dependency on main./// </summary>
        private readonly FeeRate minTrackedFee;

        /// <summary>Best seen block height.</summary>
        private int nBestSeenHeight;

        /// <summary>Setting for the node.</summary>
        private readonly MempoolSettings mempoolSettings;

        /// <summary>Logger for logging on this object.</summary>
        private readonly ILogger logger;

        /// <summary>Count of tracked transactions.</summary>
        private int trackedTxs;

        /// <summary>Count of untracked transactions.</summary>
        private int untrackedTxs;

        /// <summary>
        /// Constructs an instance of the block policy estimator object.
        /// </summary>
        /// <param name="mempoolSettings">Mempool settings.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="nodeSettings">Full node settings.</param>
        public BlockPolicyEstimator(MempoolSettings mempoolSettings, ILoggerFactory loggerFactory, NodeSettings nodeSettings)
        {
            this.mapMemPoolTxs = new Dictionary<uint256, TxStatsInfo>();
            this.mempoolSettings = mempoolSettings;
            this.nBestSeenHeight = 0;
            this.trackedTxs = 0;
            this.untrackedTxs = 0;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.minTrackedFee = nodeSettings.MinRelayTxFeeRate < new FeeRate(new Money(MinFeeRate))
                ? new FeeRate(new Money(MinFeeRate))
                : nodeSettings.MinRelayTxFeeRate;
            var vfeelist = new List<double>();
            for (double bucketBoundary = this.minTrackedFee.FeePerK.Satoshi;
                bucketBoundary <= MaxFeeRate;
                bucketBoundary *= FeeSpacing)
                vfeelist.Add(bucketBoundary);
            vfeelist.Add(InfFeeRate);
            this.feeStats = new TxConfirmStats(this.logger);
            this.feeStats.Initialize(vfeelist, MaxBlockConfirms, DefaultDecay);
        }

        /// <summary>
        /// Process all the transactions that have been included in a block.
        /// </summary>
        /// <param name="nBlockHeight">The block height for the block.</param>
        /// <param name="entries">Collection of memory pool entries.</param>
        public void ProcessBlock(int nBlockHeight, List<TxMempoolEntry> entries)
        {
            if (nBlockHeight <= this.nBestSeenHeight)
                return;

            // Must update nBestSeenHeight in sync with ClearCurrent so that
            // calls to removeTx (via processBlockTx) correctly calculate age
            // of unconfirmed txs to remove from tracking.
            this.nBestSeenHeight = nBlockHeight;

            // Clear the current block state and update unconfirmed circular buffer
            this.feeStats.ClearCurrent(nBlockHeight);

            int countedTxs = 0;
            // Repopulate the current block states
            for (int i = 0; i < entries.Count; i++)
            {
                if (this.ProcessBlockTx(nBlockHeight, entries[i]))
                    countedTxs++;
            }

            // Update all exponential averages with the current block state
            this.feeStats.UpdateMovingAverages();

            // TODO: this makes too  much noise right now, put it back when logging is can be switched on by categories (and also consider disabling during IBD)
            // Logging.Logs.EstimateFee.LogInformation(
            // $"Blockpolicy after updating estimates for {countedTxs} of {entries.Count} txs in block, since last block {trackedTxs} of {trackedTxs + untrackedTxs} tracked, new mempool map size {mapMemPoolTxs.Count}");

            this.trackedTxs = 0;
            this.untrackedTxs = 0;
        }

        /// <summary>
        /// Process a transaction confirmed in a block.
        /// </summary>
        /// <param name="nBlockHeight">Height of the block.</param>
        /// <param name="entry">The memory pool entry.</param>
        /// <returns>Whether it was able to successfully process the transaction.</returns>
        private bool ProcessBlockTx(int nBlockHeight, TxMempoolEntry entry)
        {
            if (!this.RemoveTx(entry.TransactionHash))
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
            var feeRate = new FeeRate(entry.Fee, (int)entry.GetTxSize());

            this.feeStats.Record(blocksToConfirm, feeRate.FeePerK.Satoshi);
            return true;
        }

        /// <summary>
        ///  Process a transaction accepted to the mempool.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="validFeeEstimate">Whether to update fee estimate.</param>
        public void ProcessTransaction(TxMempoolEntry entry, bool validFeeEstimate)
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
            var feeRate = new FeeRate(entry.Fee, (int)entry.GetTxSize());

            this.mapMemPoolTxs.Add(hash, new TxStatsInfo());
            this.mapMemPoolTxs[hash].blockHeight = txHeight;
            this.mapMemPoolTxs[hash].bucketIndex = this.feeStats.NewTx(txHeight, feeRate.FeePerK.Satoshi);
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
        public bool RemoveTx(uint256 hash)
        {
            TxStatsInfo pos = this.mapMemPoolTxs.TryGet(hash);
            if (pos != null)
            {
                this.feeStats.RemoveTx(pos.blockHeight, this.nBestSeenHeight, pos.bucketIndex);
                this.mapMemPoolTxs.Remove(hash);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Return a feerate estimate
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block.</param>
        public FeeRate EstimateFee(int confTarget)
        {
            // Return failure if trying to analyze a target we're not tracking
            // It's not possible to get reasonable estimates for confTarget of 1
            if (confTarget <= 1 || confTarget > this.feeStats.GetMaxConfirms())
                return new FeeRate(0);

            double median = this.feeStats.EstimateMedianVal(confTarget, SufficientFeeTxs, MinSuccessPct, true,
                this.nBestSeenHeight);

            if (median < 0)
                return new FeeRate(0);

            return new FeeRate(new Money((int)median));
        }

        /// <summary>
        /// Estimate feerate needed to be included in a block within
        /// confTarget blocks. If no answer can be given at confTarget, return an
        /// estimate at the lowest target where one can be given.
        /// </summary>
        public FeeRate EstimateSmartFee(int confTarget, ITxMempool pool, out int answerFoundAtTarget)
        {
            answerFoundAtTarget = confTarget;

            // Return failure if trying to analyze a target we're not tracking
            if (confTarget <= 0 || confTarget > this.feeStats.GetMaxConfirms())
                return new FeeRate(0);

            // It's not possible to get reasonable estimates for confTarget of 1
            if (confTarget == 1)
                confTarget = 2;

            double median = -1;
            while (median < 0 && confTarget <= this.feeStats.GetMaxConfirms())
            {
                median = this.feeStats.EstimateMedianVal(confTarget++, SufficientFeeTxs, MinSuccessPct, true,
                    this.nBestSeenHeight);
            }

            answerFoundAtTarget = confTarget - 1;

            // If mempool is limiting txs , return at least the min feerate from the mempool
            if (pool != null)
            {
                Money minPoolFee = pool.GetMinFee(this.mempoolSettings.MaxMempool * 1000000).FeePerK;
                if (minPoolFee > 0 && minPoolFee.Satoshi > median)
                    return new FeeRate(minPoolFee);
            }

            if (median < 0)
                return new FeeRate(0);

            return new FeeRate((int)median);
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

        /// <summary>
        /// Return an estimate of the priority.
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block.</param>
        /// <returns>Estimate of the priority.</returns>
        /// <remarks>TODO: Implement priority estimation</remarks>
        public double EstimatePriority(int confTarget)
        {
            return -1;
        }

        /// <summary>
        /// Return an estimated smart priority.
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block.</param>
        /// <param name="pool">Memory pool transactions.</param>
        /// <param name="answerFoundAtTarget">Block height where answer was found.</param>
        /// <returns>The smart priority.</returns>
        public double EstimateSmartPriority(int confTarget, ITxMempool pool, out int answerFoundAtTarget)
        {
            answerFoundAtTarget = confTarget;

            // If mempool is limiting txs, no priority txs are allowed
            Money minPoolFee = pool.GetMinFee(this.mempoolSettings.MaxMempool * 1000000).FeePerK;
            if (minPoolFee > 0)
                return InfPriority;

            return -1;
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
