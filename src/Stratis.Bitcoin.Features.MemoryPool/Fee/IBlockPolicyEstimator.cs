using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.MemoryPool.Fee
{
    public interface IBlockPolicyEstimator
    {
        /// <summary>
        /// Process all the transactions that have been included in a block.
        /// </summary>
        /// <param name="nBlockHeight">The block height for the block.</param>
        /// <param name="entries">Collection of memory pool entries.</param>
        void ProcessBlock(int nBlockHeight, List<TxMempoolEntry> entries);

        /// <summary>
        ///  Process a transaction accepted to the mempool.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="validFeeEstimate">Whether to update fee estimate.</param>
        void ProcessTransaction(TxMempoolEntry entry, bool validFeeEstimate);

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
        bool RemoveTx(uint256 hash, bool inBlock);

        /// <summary>
        /// Return a feerate estimate (deprecated, per Bitcoin Core source).
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block.</param>
        FeeRate EstimateFee(int confTarget);

        /// <summary>
        /// Returns the max of the feerates calculated with a 60%
        /// threshold required at target / 2, an 85% threshold required at target and a
        /// 95% threshold required at 2 * target.Each calculation is performed at the
        /// shortest time horizon which tracks the required target.Conservative
        /// estimates, however, required the 95% threshold at 2 * target be met for any
        /// longer time horizons also.
        /// </summary>
        FeeRate EstimateSmartFee(int confTarget, FeeCalculation feeCalc, bool conservative);

        /// <summary>
        /// Return a specific fee estimate calculation with a given success threshold and time horizon,
        /// and optionally return detailed data about calculation.
        /// </summary>
        /// <param name="confTarget">The desired number of confirmations to be included in a block</param>
        FeeRate EstimateRawFee(int confTarget, double successThreshold, FeeEstimateHorizon horizon, EstimationResult result);

        /// <summary>
        /// Write estimation data to a file.
        /// </summary>
        void Write();

        /// <summary>
        /// Read estimation data from a file.
        /// </summary>
        /// <param name="filein">Stream to read data from.</param>
        /// <param name="nFileVersion">Version number of the file.</param>
        bool Read();

        /// <summary>
        /// Calculation of highest target that estimates are tracked for.
        /// </summary>
        /// <param name="horizon"></param>
        int HighestTargetTracked(FeeEstimateHorizon horizon);

        /// <summary>
        /// Empty mempool transactions on shutdown to record failure to confirm for txs still in mempool.
        /// </summary>
        void FlushUnconfirmed();
    }
}