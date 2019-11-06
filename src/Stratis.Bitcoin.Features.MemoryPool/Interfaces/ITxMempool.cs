using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Fee;

namespace Stratis.Bitcoin.Features.MemoryPool.Interfaces
{
    /// <summary>
    /// Memory pool of pending transactions.
    /// </summary>
    public interface ITxMempool
    {
        /// <summary>Gets the miner policy estimator.</summary>
        BlockPolicyEstimator MinerPolicyEstimator { get; }

        /// <summary>Get the number of transactions in the memory pool.</summary>
        long Size { get; }

        /// <summary>The indexed transaction set in the memory pool.</summary>
        TxMempool.IndexedTransactionSet MapTx { get; }

        /// <summary>Collection of transaction inputs.</summary>
        List<TxMempool.NextTxPair> MapNextTx { get; }

        /// <summary>
        /// Increments number of transaction that have been updated counter.
        /// </summary>
        /// <param name="n">Number of transactions to increment by.</param>
        void AddTransactionsUpdated(int n);

        /// <summary>
        /// Add to memory pool without checking anything after calculating transaction ancestors.
        /// Must update state for all ancestors of a given transaction, to track size/count of descendant transactions.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="validFeeEstimate">Whether to update fee estimate.</param>
        /// <returns>Whether transaction was added successfully.</returns>
        /// <remarks>
        /// First version of AddUnchecked can be used to have it call CalculateMemPoolAncestors(), and
        /// then invoke the second version.
        /// </remarks>
        bool AddUnchecked(uint256 hash, TxMempoolEntry entry, bool validFeeEstimate = true);

        /// <summary>
        /// Add to memory pool without checking anything.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="setAncestors">Transaction ancestors.</param>
        /// <param name="validFeeEstimate">Whether to update fee estimate.</param>
        /// <returns>Whether transaction was added successfully.</returns>
        /// <remarks>
        /// Used by AcceptToMemoryPool(), which DOES do all the appropriate checks.
        /// </remarks>
        bool AddUnchecked(uint256 hash, TxMempoolEntry entry, TxMempool.SetEntries setAncestors, bool validFeeEstimate = true);

        /// <summary>
        /// Apply transaction priority and fee deltas.
        /// </summary>
        /// <param name="hash">Hash of the transaction.</param>
        /// <param name="dPriorityDelta">Priority delta to update.</param>
        /// <param name="nFeeDelta">Fee delta to update.</param>
        void ApplyDeltas(uint256 hash, ref double dPriorityDelta, ref Money nFeeDelta);

        /// <summary>
        /// Calculates descendants of entry that are not already in setDescendants, and adds to setDecendants.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="setDescendants">Set of entry decendants to add to.</param>
        /// <remarks>
        /// Assumes entryit is already a tx in the mempool and setMemPoolChildren
        /// is correct for tx and all descendants.
        /// Also assumes that if an entry is in setDescendants already, then all
        /// in-mempool descendants of it are already in setDescendants as well, so that we
        /// can save time by not iterating over those entries.
        /// </remarks>
        void CalculateDescendants(TxMempoolEntry entry, TxMempool.SetEntries setDescendants);

        /// <summary>
        /// Try to calculate all in-mempool ancestors of entry.
        /// (these are all calculated including the tx itself)
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="setAncestors">Set of ancestors that the ancestors are added to.</param>
        /// <param name="limitAncestorCount">Sax number of ancestorsUpdateTransactionsFromBlock.</param>
        /// <param name="limitAncestorSize">Max size of ancestors.</param>
        /// <param name="limitDescendantCount">Max number of descendants any ancestor can have.</param>
        /// <param name="limitDescendantSize">Max size of descendants any ancestor can have.</param>
        /// <param name="errString">Populated with error reason if any limits are hit.</param>
        /// <param name="fSearchForParents">Whether to search a tx's vin for in-mempool parents, or look up parents from mapLinks. Must be true for entries not in the mempool.</param>
        /// <returns>Whether operation was successful.</returns>
        bool CalculateMemPoolAncestors(TxMempoolEntry entry, TxMempool.SetEntries setAncestors, long limitAncestorCount, long limitAncestorSize, long limitDescendantCount, long limitDescendantSize, out string errString, bool fSearchForParents = true);

        /// <summary>
        ///  If sanity-checking is turned on, check makes sure the pool is consistent.
        /// (does not contain two transactions that spend the same inputs,
        /// all inputs are in the mapNextTx array). If sanity-checking is turned off,
        /// check does nothing.
        /// </summary>
        /// <param name="pcoins">Coin view of the transaction.</param>
        /// <exception cref="NotImplementedException"/>
        void Check(ICoinView pcoins);

        /// <summary>
        /// Clears the collections that contain the memory pool transactions,
        /// and increments the running total of transactions updated.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the dynamic memory usage in bytes.
        /// </summary>
        /// <returns>The dynamic memory usage value.</returns>
        long DynamicMemoryUsage();

        /// <summary>
        /// Gets the estimated fee using <see cref="MinerPolicyEstimator"/>.
        /// </summary>
        /// <param name="nBlocks">The confirmation target blocks.</param>
        /// <returns>The fee rate estimate.</returns>
        FeeRate EstimateFee(int nBlocks);

        /// <summary>
        /// Estimates the priority using <see cref="MinerPolicyEstimator"/>.
        /// </summary>
        /// <param name="nBlocks">The confirmation target blocks.</param>
        /// <returns>The estimated priority.</returns>
        double EstimatePriority(int nBlocks);

        /// <summary>
        /// Estimates the smart fee using <see cref="MinerPolicyEstimator"/>.
        /// </summary>
        /// <param name="nBlocks">The confirmation target blocks.</param>
        /// <param name="answerFoundAtBlocks">The block where the fee was found.</param>
        /// <returns>The fee rate estimate.</returns>
        FeeRate EstimateSmartFee(int nBlocks, out int answerFoundAtBlocks);

        /// <summary>
        /// Estimates the smart priority using <see cref="MinerPolicyEstimator"/>.
        /// </summary>
        /// <param name="nBlocks">The confirmation target blocks.</param>
        /// <param name="answerFoundAtBlocks">The block where the priority was found.</param>
        /// <returns>The estimated priority.</returns>
        double EstimateSmartPriority(int nBlocks, out int answerFoundAtBlocks);

        /// <summary>
        /// Whether the transaction hash exists in the memory pool.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        /// <returns>Whether the transaction exists.</returns>
        bool Exists(uint256 hash);

        /// <summary>
        /// Expire all transaction (and their dependencies) in the mempool older than time.
        /// </summary>
        /// <param name="time">Expiry time.</param>
        /// <returns>Return the number of removed transactions.</returns>
        int Expire(long time);

        /// <summary>
        /// Gets the transaction from the memory pool based upon the transaction hash.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        /// <returns>The transaction.</returns>
        Transaction Get(uint256 hash);

        /// <summary>
        /// The minimum fee to get into the mempool, which may itself not be enough for larger-sized transactions.
        /// </summary>
        /// <param name="sizelimit">Size limit of the memory pool in bytes.</param>
        /// <returns>The minimum fee.</returns>
        /// <remarks>
        /// The minReasonableRelayFee constructor arg is used to bound the time it
        /// takes the fee rate to go back down all the way to 0. When the feerate
        /// would otherwise be half of this, it is set to 0 instead.
        /// </remarks>
        FeeRate GetMinFee(long sizelimit);

        /// <summary>
        /// Get number of transactions that have been updated.
        /// </summary>
        /// <returns>Number of transactions.</returns>
        int GetTransactionsUpdated();

        /// <summary>
        /// Check that none of this transactions inputs are in the mempool, and thus
        /// the tx is not dependent on other mempool transactions to be included in a block.
        /// </summary>
        /// <param name="tx">The transaction to check.</param>
        /// <returns>Whether the transaction is not dependent on other transaction.</returns>
        bool HasNoInputsOf(Transaction tx);

        /// <summary>
        /// Read fee estimates from a stream.
        /// </summary>
        /// <param name="stream">Stream to read from.</param>
        void ReadFeeEstimates(BitcoinStream stream);

        /// <summary>
        /// Called when a block is connected. Removes transactions from mempool and updates the miner fee estimator.
        /// </summary>
        /// <param name="vtx">Collection of transactions.</param>
        /// <param name="blockHeight">Height to connect the block.</param>
        void RemoveForBlock(IEnumerable<Transaction> vtx, int blockHeight);

        /// <summary>
        /// Removes the transaction from the memory pool recursively.
        /// </summary>
        /// <param name="origTx">The original transaction to remove.</param>
        void RemoveRecursive(Transaction origTx);

        /// <summary>
        /// Remove a set of transactions from the mempool.
        /// </summary>
        /// <param name="stage">Staged transactions.</param>
        /// <param name="updateDescendants">Whether to update decendants.</param>
        /// <remarks>
        /// If a transaction is in this set, then all in-mempool descendants must
        /// also be in the set, unless this transaction is being removed for being
        /// in a block.
        /// Set updateDescendants to true when removing a tx that was in a block, so
        /// that any in-mempool descendants have their ancestor state updated.
        /// </remarks>
        void RemoveStaged(TxMempool.SetEntries stage, bool updateDescendants);

        /// <summary>
        /// Set how frequent the sanity check is executed.
        /// </summary>
        /// <param name="dFrequency">The frequency of the sanity check.</param>
        void SetSanityCheck(double dFrequency = 1);

        /// <summary>
        /// Trims the memory pool to a size limite.
        /// </summary>
        /// <param name="sizelimit">Size limit to trim memory pool to.</param>
        /// <param name="pvNoSpendsRemaining">Collection of no spends transactions remaining.</param>
        void TrimToSize(long sizelimit, List<uint256> pvNoSpendsRemaining = null);

        /// <summary>
        /// Write fee estimates to a stream.
        /// </summary>
        /// <param name="stream">Stream to write to.</param>
        void WriteFeeEstimates(BitcoinStream stream);
    }
}