using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.MemoryPool.Fee
{
    //  CBlockPolicyEstimator

    // The BlockPolicyEstimator is used for estimating the feerate needed
    // for a transaction to be included in a block within a certain number of
    // blocks.
    //
    // At a high level the algorithm works by grouping transactions into buckets
    // based on having similar feerates and then tracking how long it
    // takes transactions in the various buckets to be mined.  It operates under
    // the assumption that in general transactions of higher feerate will be
    // included in blocks before transactions of lower feerate.   So for
    // example if you wanted to know what feerate you should put on a transaction to
    // be included in a block within the next 5 blocks, you would start by looking
    // at the bucket with the highest feerate transactions and verifying that a
    // sufficiently high percentage of them were confirmed within 5 blocks and
    // then you would look at the next highest feerate bucket, and so on, stopping at
    // the last bucket to pass the test.   The average feerate of transactions in this
    // bucket will give you an indication of the lowest feerate you can put on a
    // transaction and still have a sufficiently high chance of being confirmed
    // within your desired 5 blocks.
    //
    // Here is a brief description of the implementation:
    // When a transaction enters the mempool, we
    // track the height of the block chain at entry.  Whenever a block comes in,
    // we count the number of transactions in each bucket and the total amount of feerate
    // paid in each bucket. Then we calculate how many blocks Y it took each
    // transaction to be mined and we track an array of counters in each bucket
    // for how long it to took transactions to get confirmed from 1 to a max of 25
    // and we increment all the counters from Y up to 25. This is because for any
    // number Z>=Y the transaction was successfully mined within Z blocks.  We
    // want to save a history of this information, so at any time we have a
    // counter of the total number of transactions that happened in a given feerate
    // bucket and the total number that were confirmed in each number 1-25 blocks
    // or less for any bucket.   We save this history by keeping an exponentially
    // decaying moving average of each one of these stats.  Furthermore we also
    // keep track of the number unmined (in mempool) transactions in each bucket
    // and for how many blocks they have been outstanding and use that to increase
    // the number of transactions we've seen in that feerate bucket when calculating
    // an estimate for any number of confirmations below the number of blocks
    // they've been outstanding.

    // We will instantiate an instance of this class to track transactions that were
    // included in a block. We will lump transactions into a bucket according to their
    // approximate feerate and then track how long it took for those txs to be included in a block
    //
    // The tracking of unconfirmed (mempool) transactions is completely independent of the
    // historical tracking of transactions that have been confirmed in a block.

    //  We want to be able to estimate feerates that are needed on tx's to be included in
    // a certain number of blocks.Every time a block is added to the best chain, this class records
    // stats on the transactions included in that block
    public class BlockPolicyEstimator
    {
        // Require an avg of 1 tx in the combined feerate bucket per block to have stat significance 
        private const double SUFFICIENT_FEETXS = 1;

        // Require greater than 95% of X feerate transactions to be confirmed within Y blocks for X to be big enough 
        private const double MIN_SUCCESS_PCT = .95;

        // Minimum and Maximum values for tracking feerates
        private const long MIN_FEERATE = 10;

        private const double MAX_FEERATE = 1e7;

        // We have to lump transactions into buckets based on feerate, but we want to be able
        // to give accurate estimates over a large range of potential feerates
        // Therefore it makes sense to exponentially space the buckets
        /** Spacing of FeeRate buckets */
        private const double FEE_SPACING = 1.1;

        /** Track confirm delays up to 25 blocks, can't estimate beyond that */
        private const int MAX_BLOCK_CONFIRMS = 25;

        /** Decay of .998 is a half-life of 346 blocks or about 2.4 days */
        private const double DEFAULT_DECAY = .998;

        public const double INF_PRIORITY = 1e9 * 21000000ul * Money.COIN;

        private static readonly Money MAX_MONEY = new Money(21000000 * Money.COIN);
        private static readonly double INF_FEERATE = MAX_MONEY.Satoshi;

        // Classes to track historical data on transaction confirmations 
        private readonly TxConfirmStats feeStats;

        // map of txids to information about that transaction
        private readonly Dictionary<uint256, TxStatsInfo> mapMemPoolTxs;

        private readonly FeeRate minTrackedFee; //!< Passed to constructor to avoid dependency on main
        private int nBestSeenHeight;

        private readonly NodeSettings nodeArgs;
        private readonly ILogger logger;

        private int trackedTxs;
        private int untrackedTxs;

        public BlockPolicyEstimator(FeeRate minRelayFee, NodeSettings nodeArgs, ILoggerFactory loggerFactory)
        {
            this.mapMemPoolTxs = new Dictionary<uint256, TxStatsInfo>();
            this.nodeArgs = nodeArgs;
            this.nBestSeenHeight = 0;
            this.trackedTxs = 0;
            this.untrackedTxs = 0;
            this.logger = loggerFactory.CreateLogger<BlockPolicyEstimator>();

            this.minTrackedFee = minRelayFee < new FeeRate(new Money(MIN_FEERATE))
                ? new FeeRate(new Money(MIN_FEERATE))
                : minRelayFee;
            var vfeelist = new List<double>();
            for (double bucketBoundary = this.minTrackedFee.FeePerK.Satoshi;
                bucketBoundary <= MAX_FEERATE;
                bucketBoundary *= FEE_SPACING)
                vfeelist.Add(bucketBoundary);
            vfeelist.Add(INF_FEERATE);
            this.feeStats = new TxConfirmStats(this.logger);
            this.feeStats.Initialize(vfeelist, MAX_BLOCK_CONFIRMS, DEFAULT_DECAY);
        }

        // Process all the transactions that have been included in a block 
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

            var countedTxs = 0;
            // Repopulate the current block states
            for (var i = 0; i < entries.Count; i++)
                if (this.ProcessBlockTx(nBlockHeight, entries[i]))
                    countedTxs++;

            // Update all exponential averages with the current block state
            this.feeStats.UpdateMovingAverages();

            // TODO: this makes too  much noise right now, put it back when logging is can be switched on by categories (and also consider disabling during IBD)
            //Logging.Logs.EstimateFee.LogInformation(
            //	$"Blockpolicy after updating estimates for {countedTxs} of {entries.Count} txs in block, since last block {trackedTxs} of {trackedTxs + untrackedTxs} tracked, new mempool map size {mapMemPoolTxs.Count}");

            this.trackedTxs = 0;
            this.untrackedTxs = 0;
        }

        // Process a transaction confirmed in a block
        private bool ProcessBlockTx(int nBlockHeight, TxMempoolEntry entry)
        {
            if (!this.RemoveTx(entry.TransactionHash))
                return false;

            // How many blocks did it take for miners to include this transaction?
            // blocksToConfirm is 1-based, so a transaction included in the earliest
            // possible block has confirmation count of 1
            var blocksToConfirm = nBlockHeight - entry.EntryHeight;
            if (blocksToConfirm <= 0)
            {
                // This can't happen because we don't process transactions from a block with a height
                // lower than our greatest seen height
                this.logger.LogInformation($"Blockpolicy error Transaction had negative blocksToConfirm");
                return false;
            }

            // Feerates are stored and reported as BTC-per-kb:
            var feeRate = new FeeRate(entry.Fee, (int) entry.GetTxSize());

            this.feeStats.Record(blocksToConfirm, feeRate.FeePerK.Satoshi);
            return true;
        }

        // Process a transaction accepted to the mempool
        public void ProcessTransaction(TxMempoolEntry entry, bool validFeeEstimate)
        {
            var txHeight = entry.EntryHeight;
            var hash = entry.TransactionHash;
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
            var feeRate = new FeeRate(entry.Fee, (int) entry.GetTxSize());

            this.mapMemPoolTxs.Add(hash, new TxStatsInfo());
            this.mapMemPoolTxs[hash].blockHeight = txHeight;
            this.mapMemPoolTxs[hash].bucketIndex = this.feeStats.NewTx(txHeight, feeRate.FeePerK.Satoshi);
        }

        // Remove a transaction from the mempool tracking stats
        // This function is called from CTxMemPool::removeUnchecked to ensure
        // txs removed from the mempool for any reason are no longer
        // tracked. Txs that were part of a block have already been removed in
        // processBlockTx to ensure they are never double tracked, but it is
        // of no harm to try to remove them again.
        public bool RemoveTx(uint256 hash)
        {
            var pos = this.mapMemPoolTxs.TryGet(hash);
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

            var median = this.feeStats.EstimateMedianVal(confTarget, SUFFICIENT_FEETXS, MIN_SUCCESS_PCT, true,
                this.nBestSeenHeight);

            if (median < 0)
                return new FeeRate(0);

            return new FeeRate(new Money((int) median));
        }

        /// <summary>
        /// Estimate feerate needed to be included in a block within
        /// confTarget blocks. If no answer can be given at confTarget, return an
        /// estimate at the lowest target where one can be given.
        /// </summary>
        public FeeRate EstimateSmartFee(int confTarget, TxMempool pool, out int answerFoundAtTarget)
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
                median = this.feeStats.EstimateMedianVal(confTarget++, SUFFICIENT_FEETXS, MIN_SUCCESS_PCT, true,
                    this.nBestSeenHeight);

            answerFoundAtTarget = confTarget - 1;

            // If mempool is limiting txs , return at least the min feerate from the mempool
            if (pool != null)
            {
                var minPoolFee = pool.GetMinFee(this.nodeArgs.Mempool.MaxMempool * 1000000).FeePerK;
                if (minPoolFee > 0 && minPoolFee.Satoshi > median)
                    return new FeeRate(minPoolFee);
            }

            if (median < 0)
                return new FeeRate(0);

            return new FeeRate((int) median);
        }

        // Write estimation data to a file 
        public void Write(Stream fileout)
        {
        }

        // Read estimation data from a file 
        public void Read(Stream filein, int nFileVersion)
        {
        }

        public double EstimatePriority(int confTarget)
        {
            return -1;
        }

        public double EstimateSmartPriority(int confTarget, TxMempool pool, out int answerFoundAtTarget)
        {
            answerFoundAtTarget = confTarget;


            // If mempool is limiting txs, no priority txs are allowed
            var minPoolFee = pool.GetMinFee(this.nodeArgs.Mempool.MaxMempool * 1000000).FeePerK;
            if (minPoolFee > 0)
                return INF_PRIORITY;

            return -1;
        }

        public class TxStatsInfo
        {
            public int blockHeight;
            public int bucketIndex;

            public TxStatsInfo()
            {
                this.blockHeight = 0;
                this.bucketIndex = 0;
            }
        }
    }
}