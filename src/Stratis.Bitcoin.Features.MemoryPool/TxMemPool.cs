using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool.Fee;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Information about a memory pool transaction.
    /// </summary>
    public class TxMempoolInfo
    {
        /// <summary>The transaction itself.</summary>
        public Transaction Trx { get; set; }

        /// <summary>Time the transaction entered the mempool.</summary>
        public long Time { get; set; }

        /// <summary>Fee rate of the transaction.</summary>
        public FeeRate FeeRate { get; set; }

        /// <summary>The fee delta.</summary>
        public long FeeDelta { get; set; }
    }

;

    /// <summary>
    /// Memory pool of pending transactions.
    /// </summary>
    /// <remarks>
    ///
    /// TxMempool stores valid-according-to-the-current-best-chain transactions
    /// that may be included in the next block.
    ///
    /// Transactions are added when they are seen on the network(or created by the
    /// local node), but not all transactions seen are added to the pool.For
    /// example, the following new transactions will not be added to the mempool:
    /// - a transaction which doesn't make the mimimum fee requirements.
    /// - a new transaction that double-spends an input of a transaction already in
    /// the pool where the new transaction does not meet the Replace-By-Fee
    /// requirements as defined in BIP 125.
    /// - a non-standard transaction.
    ///
    /// <see cref="TxMempool.MapTx"/>, and <see cref="TxMempoolEntry"/> bookkeeping:
    ///
    /// <see cref="MapTx"/> is a collection that sorts the mempool on 4 criteria:
    /// - transaction hash
    /// - feerate[we use max(feerate of tx, feerate of Transaction with all descendants)]
    /// - time in mempool
    /// - mining score (feerate modified by any fee deltas from PrioritiseTransaction)
    ///
    /// Note: the term "descendant" refers to in-mempool transactions that depend on
    /// this one, while "ancestor" refers to in-mempool transactions that a given
    /// transaction depends on.
    ///
    /// In order for the feerate sort to remain correct, we must update transactions
    /// in the mempool when new descendants arrive. To facilitate this, we track
    /// the set of in-mempool direct parents and direct children in <see cref="mapLinks.Within"/>
    /// each TxMempoolEntry, we track the size and fees of all descendants.
    ///
    /// Usually when a new transaction is added to the mempool, it has no in-mempool
    /// children(because any such children would be an orphan).  So in
    /// <see cref="AddUnchecked(uint256, TxMempoolEntry, bool)"/>, we:
    /// - update a new entry's setMemPoolParents to include all in-mempool parents
    /// - update the new entry's direct parents to include the new tx as a child
    /// - update all ancestors of the transaction to include the new tx's size/fee
    ///
    /// When a transaction is removed from the mempool, we must:
    /// - update all in-mempool parents to not track the tx in setMemPoolChildren
    /// - update all ancestors to not include the tx's size/fees in descendant state
    /// - update all in-mempool children to not include it as a parent
    ///
    /// These happen in <see cref="UpdateForRemoveFromMempool(TxMempool.SetEntries, bool)"/>.
    /// (Note that when removing a
    /// transaction along with its descendants, we must calculate that set of
    /// transactions to be removed before doing the removal, or else the mempool can
    /// be in an inconsistent state where it's impossible to walk the ancestors of
    /// a transaction.)
    ///
    /// In the event of a reorg, the assumption that a newly added tx has no
    /// in-mempool children is false.  In particular, the mempool is in an
    /// inconsistent state while new transactions are being added, because there may
    /// be descendant transactions of a tx coming from a disconnected block that are
    /// unreachable from just looking at transactions in the mempool(the linking
    /// transactions may also be in the disconnected block, waiting to be added).
    /// Because of this, there's not much benefit in trying to search for in-mempool
    /// children in <see cref="AddUnchecked(uint256, TxMempoolEntry, SetEntries, bool)"/>.
    /// Instead, in the special case of transactions
    /// being added from a disconnected block, we require the caller to clean up the
    /// state, to account for in-mempool, out-of-block descendants for all the
    /// in-block transactions by calling <see cref="AddTransactionsUpdated(int)"/>.  Note that
    /// until this is called, the mempool state is not consistent, and in particular
    /// <see cref="mapLinks"/> may not be correct (and therefore functions like
    /// <see cref="CalculateMemPoolAncestors(TxMempoolEntry, TxMempool.SetEntries, long, long, long, long, out string, bool)"/>
    /// and <see cref="CalculateDescendants(TxMempoolEntry, TxMempool.SetEntries)"/> that rely
    /// on them to walk the mempool are not generally safe to use).
    ///
    /// Computational limits:
    ///
    /// Updating all in-mempool ancestors of a newly added transaction can be slow,
    /// if no bound exists on how many in-mempool ancestors there may be.
    /// <see cref="CalculateMemPoolAncestors(TxMempoolEntry, TxMempool.SetEntries, long, long, long, long, out string, bool)"/>
    /// takes configurable limits that are designed to
    /// prevent these calculations from being too CPU intensive.
    ///
    /// Adding transactions from a disconnected block can be very time consuming,
    /// because we don't have a way to limit the number of in-mempool descendants.
    /// To bound CPU processing, we limit the amount of work we're willing to do
    /// to properly update the descendant information for a tx being added from
    /// a disconnected block.  If we would exceed the limit, then we instead mark
    /// the entry as "dirty", and set the feerate for sorting purposes to be equal
    /// the feerate of the transaction without any descendants.
    /// </remarks>
    public class TxMempool : ITxMempool
    {
        /// <summary>Fake height value used in Coins to signify they are only in the memory pool (since 0.8).</summary>
        public const int MempoolHeight = NetworkExtensions.MempoolHeight;

        /// <summary>The rolling fee's half life.</summary>
        public const int RollingFeeHalflife = 60 * 60 * 12;// public only for testing.

        /// <summary>The indexed transaction set in the memory pool.</summary>
        public IndexedTransactionSet MapTx { get; }

        /// <summary>Collection of transaction inputs.</summary>
        public List<NextTxPair> MapNextTx { get; }

        /// <summary>Value n means that n times in 2^32 we check.</summary>
        private double checkFrequency;

        /// <summary>Number of transactions updated.</summary>
        private int nTransactionsUpdated;

        /// <summary>
        ///  Sum of all mempool tx's virtual sizes.
        ///  Differs from serialized Transaction size since witness data is discounted. Defined in BIP 141.
        /// </summary>
        private long totalTxSize;

        /// <summary>Sum of dynamic memory usage of all the map elements (NOT the maps themselves).</summary>
        private long cachedInnerUsage;

        /// <summary>
        /// minReasonableRelayFee should be a feerate which is, roughly, somewhere
        /// around what it "costs" to relay a transaction around the network and
        /// below which we would reasonably say a transaction has 0-effective-fee.
        ///  </summary>
        private readonly FeeRate minReasonableRelayFee;

        /// <summary>Time when the last rolling fee was updated.</summary>
        private long lastRollingFeeUpdate;

        /// <summary>Whether are new blocks since last rolling fee update.</summary>
        private bool blockSinceLastRollingFeeBump;

        /// <summary>minimum fee to get into the pool, decreases exponentially.</summary>
        private double rollingMinimumFeeRate;

        /// <summary>Collection of transaction links.</summary>
        private TxlinksMap mapLinks;

        /// <summary>Dictionary of <see cref="DeltaPair"/> indexed by transaction hash.</summary>
        private Dictionary<uint256, DeltaPair> mapDeltas;

        /// <summary>All tx witness hashes/entries in mapTx, in random order.</summary>
        private Dictionary<TxMempoolEntry, uint256> vTxHashes;

        /// <summary>Instance logger for the memory pool.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Constructs a new TxMempool object.
        /// </summary>
        /// <param name="dateTimeProvider">The data and time provider for accessing current date and time.</param>
        /// <param name="blockPolicyEstimator">The block policy estimator object.</param>
        /// <param name="loggerFactory">Factory for creating instance logger.</param>
        /// <param name="nodeSettings">Full node settings.</param>
        public TxMempool(IDateTimeProvider dateTimeProvider, BlockPolicyEstimator blockPolicyEstimator, ILoggerFactory loggerFactory, NodeSettings nodeSettings)
        {
            this.MapTx = new IndexedTransactionSet();
            this.mapLinks = new TxlinksMap();
            this.MapNextTx = new List<NextTxPair>();
            this.mapDeltas = new Dictionary<uint256, DeltaPair>();
            this.vTxHashes = new Dictionary<TxMempoolEntry, uint256>(); // All tx witness hashes/entries in mapTx, in random order.
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.TimeProvider = dateTimeProvider;
            this.InnerClear(); //lock free clear

            // Sanity checks off by default for performance, because otherwise
            // accepting transactions becomes O(N^2) where N is the number
            // of transactions in the pool
            this.checkFrequency = 0;

            this.MinerPolicyEstimator = blockPolicyEstimator;
            this.minReasonableRelayFee = nodeSettings.MinRelayTxFeeRate;
        }

        /// <summary>Gets the miner policy estimator.</summary>
        public BlockPolicyEstimator MinerPolicyEstimator { get; }

        /// <summary>Get the number of transactions in the memory pool.</summary>
        public long Size
        {
            get { return this.MapTx.Count; }
        }

        /// <summary>Gets the date and time provider.</summary>
        private IDateTimeProvider TimeProvider { get; }

        /// <summary>
        /// Clears the collections that contain the memory pool transactions,
        /// and increments the running total of transactions updated.
        /// </summary>
        private void InnerClear()
        {
            this.mapLinks.Clear();
            this.MapTx.Clear();
            this.MapNextTx.Clear();
            this.totalTxSize = 0;
            this.cachedInnerUsage = 0;
            this.lastRollingFeeUpdate = this.TimeProvider.GetTime();
            this.blockSinceLastRollingFeeBump = false;
            this.rollingMinimumFeeRate = 0;
            ++this.nTransactionsUpdated;
        }


        /// <inheritdoc />
        public void Clear()
        {
            //LOCK(cs);
            this.InnerClear();
        }

        /// <summary>
        /// Set the new memory pools min fee to the fee rate of the removed set.
        /// </summary>
        /// <param name="rate">Fee rate of the removed set</param>
        private void trackPackageRemoved(FeeRate rate)
        {
            // candidate for async
            //AssertLockHeld(cs);

            if (rate.FeePerK.Satoshi > this.rollingMinimumFeeRate)
            {
                this.rollingMinimumFeeRate = rate.FeePerK.Satoshi;
                this.blockSinceLastRollingFeeBump = false;
            }
        }

        /// <inheritdoc />
        public void Check(ICoinView pcoins)
        {
            if (this.checkFrequency == 0)
                return;

            if (new Random(int.MaxValue).Next() >= this.checkFrequency)
                return;

            this.logger.LogInformation($"Checking mempool with {this.MapTx.Count} transactions and {this.MapNextTx.Count} inputs");

            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public Transaction Get(uint256 hash)
        {
            return this.MapTx.TryGet(hash)?.Transaction;
        }

        /// <inheritdoc />
        public FeeRate EstimateFee(int nBlocks)
        {
            return this.MinerPolicyEstimator.EstimateFee(nBlocks);
        }

        /// <inheritdoc />
        public FeeRate EstimateSmartFee(int nBlocks, out int answerFoundAtBlocks)
        {
            return this.MinerPolicyEstimator.EstimateSmartFee(nBlocks, this, out answerFoundAtBlocks);
        }

        /// <inheritdoc />
        public double EstimatePriority(int nBlocks)
        {
            return this.MinerPolicyEstimator.EstimatePriority(nBlocks);
        }

        /// <inheritdoc />
        public double EstimateSmartPriority(int nBlocks, out int answerFoundAtBlocks)
        {
            return this.MinerPolicyEstimator.EstimateSmartPriority(nBlocks, this, out answerFoundAtBlocks);
        }

        /// <inheritdoc />
        public void SetSanityCheck(double dFrequency = 1.0)
        {
            this.checkFrequency = dFrequency * 4294967295.0;
        }

        /// <inheritdoc />
        public bool AddUnchecked(uint256 hash, TxMempoolEntry entry, bool validFeeEstimate = true)
        {
            //LOCK(cs);
            var setAncestors = new SetEntries();
            long nNoLimit = long.MaxValue;
            string dummy;
            this.CalculateMemPoolAncestors(entry, setAncestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy);
            bool returnVal = this.AddUnchecked(hash, entry, setAncestors, validFeeEstimate);
            
            return returnVal;
        }

        /// <inheritdoc />
        public bool AddUnchecked(uint256 hash, TxMempoolEntry entry, SetEntries setAncestors, bool validFeeEstimate = true)
        {
            //LOCK(cs);
            this.MapTx.Add(entry);
            this.mapLinks.Add(entry, new TxLinks { Parents = new SetEntries(), Children = new SetEntries() });

            // Update transaction for any feeDelta created by PrioritiseTransaction
            // TODO: refactor so that the fee delta is calculated before inserting
            // into mapTx.
            DeltaPair pos = this.mapDeltas.TryGet(hash);
            if (pos != null)
            {
                if (pos.Amount != null)
                {
                    entry.UpdateFeeDelta(pos.Amount.Satoshi);
                }
            }

            // Update cachedInnerUsage to include contained transaction's usage.
            // (When we update the entry for in-mempool parents, memory usage will be
            // further updated.)
            this.cachedInnerUsage += entry.DynamicMemoryUsage();

            Transaction tx = entry.Transaction;
            var setParentTransactions = new HashSet<uint256>();
            foreach (TxIn txInput in tx.Inputs)
            {
                this.MapNextTx.Add(new NextTxPair { OutPoint = txInput.PrevOut, Transaction = tx });
                setParentTransactions.Add(txInput.PrevOut.Hash);
            }
            // Don't bother worrying about child transactions of this one.
            // Normal case of a new transaction arriving is that there can't be any
            // children, because such children would be orphans.
            // An exception to that is if a transaction enters that used to be in a block.
            // In that case, our disconnect block logic will call UpdateTransactionsFromBlock
            // to clean up the mess we're leaving here.

            // Update ancestors with information about this tx
            foreach (uint256 phash in setParentTransactions)
            {
                TxMempoolEntry pit = this.MapTx.TryGet(phash);
                if (pit != null)
                    this.UpdateParent(entry, pit, true);
            }

            this.UpdateAncestorsOf(true, entry, setAncestors);
            this.UpdateEntryForAncestors(entry, setAncestors);

            this.nTransactionsUpdated++;
            this.totalTxSize += entry.GetTxSize();

            this.MinerPolicyEstimator.ProcessTransaction(entry, validFeeEstimate);

            this.vTxHashes.Add(entry, tx.GetWitHash());
            //entry.vTxHashesIdx = vTxHashes.size() - 1;

            return true;
        }

        /// <summary>
        /// Set ancestor state for an entry.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="setAncestors">Transaction ancestors.</param>
        private void UpdateEntryForAncestors(TxMempoolEntry entry, SetEntries setAncestors)
        {
            long updateCount = setAncestors.Count;
            long updateSize = 0;
            Money updateFee = 0;
            long updateSigOpsCost = 0;
            foreach (TxMempoolEntry ancestorIt in setAncestors)
            {
                updateSize += ancestorIt.GetTxSize();
                updateFee += ancestorIt.ModifiedFee;
                updateSigOpsCost += ancestorIt.SigOpCost;
            }
            entry.UpdateAncestorState(updateSize, updateFee, updateCount, updateSigOpsCost);
        }

        /// <summary>
        /// Update ancestors of hash to add/remove it as a descendant transaction.
        /// </summary>
        /// <param name="add">Whether to add or remove.</param>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="setAncestors">Transaction ancestors.</param>
        private void UpdateAncestorsOf(bool add, TxMempoolEntry entry, SetEntries setAncestors)
        {
            SetEntries parentIters = this.GetMemPoolParents(entry);
            // add or remove this tx as a child of each parent
            foreach (TxMempoolEntry piter in parentIters)
                this.UpdateChild(piter, entry, add);

            long updateCount = (add ? 1 : -1);
            long updateSize = updateCount * entry.GetTxSize();
            Money updateFee = updateCount * entry.ModifiedFee;
            foreach (TxMempoolEntry ancestorIt in setAncestors)
            {
                ancestorIt.UpdateDescendantState(updateSize, updateFee, updateCount);
            }
        }

        /// <summary>
        /// Gets the parents of a memory pool entry.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <returns>Set of parent entries.</returns>
        private SetEntries GetMemPoolParents(TxMempoolEntry entry)
        {
            Guard.NotNull(entry, nameof(entry));

            Guard.Assert(this.MapTx.ContainsKey(entry.TransactionHash));
            TxLinks it = this.mapLinks.TryGet(entry);
            Guard.Assert(it != null);

            return it.Parents;
        }

        /// <summary>
        /// Gets the children of a memory pool entry.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <returns>Set of child entries.</returns>
        private SetEntries GetMemPoolChildren(TxMempoolEntry entry)
        {
            Guard.NotNull(entry, nameof(entry));

            Guard.Assert(this.MapTx.ContainsKey(entry.TransactionHash));
            TxLinks it = this.mapLinks.TryGet(entry);
            Guard.Assert(it != null);
            
            return it.Children;
        }

        /// <summary>
        /// Updates memory pool entry with a child.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="child">Child entry to add/remove.</param>
        /// <param name="add">Whether to add or remove entry.</param>
        private void UpdateChild(TxMempoolEntry entry, TxMempoolEntry child, bool add)
        {
            // todo: find how to take a memory size of SetEntries
            //setEntries s;
            if (add && this.mapLinks[entry].Children.Add(child))
            {
                this.cachedInnerUsage += child.DynamicMemoryUsage();
            }
            else if (!add && this.mapLinks[entry].Children.Remove(child))
            {
                this.cachedInnerUsage -= child.DynamicMemoryUsage();
            }
        }

        /// <summary>
        /// Updates memory pool entry with a parent.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        /// <param name="parent">Parent entry to add/remove.</param>
        /// <param name="add">Whether to add or remove entry.</param>
        private void UpdateParent(TxMempoolEntry entry, TxMempoolEntry parent, bool add)
        {
            // todo: find how to take a memory size of SetEntries
            //SetEntries s;
            if (add && this.mapLinks[entry].Parents.Add(parent))
            {
                this.cachedInnerUsage += parent.DynamicMemoryUsage();
            }
            else if (!add && this.mapLinks[entry].Parents.Remove(parent))
            {
                this.cachedInnerUsage -= parent.DynamicMemoryUsage();
            }
        }

        /// <inheritdoc />
        public bool CalculateMemPoolAncestors(TxMempoolEntry entry, SetEntries setAncestors, long limitAncestorCount,
            long limitAncestorSize, long limitDescendantCount, long limitDescendantSize, out string errString,
            bool fSearchForParents = true)
        {
            errString = string.Empty;
            var parentHashes = new SetEntries();
            Transaction tx = entry.Transaction;

            if (fSearchForParents)
            {
                // Get parents of this transaction that are in the mempool
                // GetMemPoolParents() is only valid for entries in the mempool, so we
                // iterate mapTx to find parents.
                foreach (TxIn txInput in tx.Inputs)
                {
                    TxMempoolEntry piter = this.MapTx.TryGet(txInput.PrevOut.Hash);
                    if (piter != null)
                    {
                        parentHashes.Add(piter);
                        if (parentHashes.Count + 1 > limitAncestorCount)
                        {
                            errString = $"too many unconfirmed parents [limit: {limitAncestorCount}]";
                            this.logger.LogTrace("(-)[TOO_MANY_UNCONFIRM_PARENTS]:false");
                            return false;
                        }
                    }
                }
            }
            else
            {
                if (this.MapTx.ContainsKey(entry.TransactionHash))
                {
                    // If we're not searching for parents, we require this to be an
                    // entry in the mempool already.
                    //var it = mapTx.Txids.TryGet(entry.TransactionHash);
                    SetEntries memPoolParents = this.GetMemPoolParents(entry);
                    foreach (TxMempoolEntry item in memPoolParents)
                        parentHashes.Add(item);
                }
            }

            long totalSizeWithAncestors = entry.GetTxSize();

            while (parentHashes.Any())
            {
                TxMempoolEntry stageit = parentHashes.First();

                setAncestors.Add(stageit);
                parentHashes.Remove(stageit);
                totalSizeWithAncestors += stageit.GetTxSize();

                if (stageit.SizeWithDescendants + entry.GetTxSize() > limitDescendantSize)
                {
                    errString = $"exceeds descendant size limit for tx {stageit.TransactionHash} [limit: {limitDescendantSize}]";
                    this.logger.LogTrace("(-)[EXCEED_DECENDANT_SIZE_LIMIT]:false");
                    return false;
                }
                else if (stageit.CountWithDescendants + 1 > limitDescendantCount)
                {
                    errString = $"too many descendants for tx {stageit.TransactionHash} [limit: {limitDescendantCount}]";
                    this.logger.LogTrace("(-)[TOO_MANY_DECENDANTS]:false");
                    return false;
                }
                else if (totalSizeWithAncestors > limitAncestorSize)
                {
                    errString = $"exceeds ancestor size limit [limit: {limitAncestorSize}]";
                    this.logger.LogTrace("(-)[EXCEED_ANCESTOR_SIZE_LIMIT]:false");
                    return false;
                }

                SetEntries setMemPoolParents = this.GetMemPoolParents(stageit);
                foreach (TxMempoolEntry phash in setMemPoolParents)
                {
                    // If this is a new ancestor, add it.
                    if (!setAncestors.Contains(phash))
                    {
                        parentHashes.Add(phash);
                    }
                    if (parentHashes.Count + setAncestors.Count + 1 > limitAncestorCount)
                    {
                        errString = $"too many unconfirmed ancestors [limit: {limitAncestorCount}]";
                        this.logger.LogTrace("(-)[TOO_MANY_UNCONFIRM_ANCESTORS]:false");
                        return false;
                    }
                }
            }
            
            return true;
        }

        /// <inheritdoc />
        public bool HasNoInputsOf(Transaction tx)
        {
            foreach (TxIn txInput in tx.Inputs)
            {
                if (this.Exists(txInput.PrevOut.Hash))
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <inheritdoc />
        public bool Exists(uint256 hash)
        {
            bool returnVal = this.MapTx.ContainsKey(hash);

            return returnVal;
        }

        /// <inheritdoc />
        public void RemoveRecursive(Transaction origTx)
        {
            // Remove transaction from memory pool
            uint256 origHahs = origTx.GetHash();

            var txToRemove = new SetEntries();
            TxMempoolEntry origit = this.MapTx.TryGet(origHahs);
            if (origit != null)
            {
                txToRemove.Add(origit);
            }
            else
            {
                // When recursively removing but origTx isn't in the mempool
                // be sure to remove any children that are in the pool. This can
                // happen during chain re-orgs if origTx isn't re-accepted into
                // the mempool for any reason.
                for (int i = 0; i < origTx.Outputs.Count; i++)
                {
                    NextTxPair it = this.MapNextTx.FirstOrDefault(w => w.OutPoint == new OutPoint(origHahs, i));
                    if (it == null)
                        continue;
                    TxMempoolEntry nextit = this.MapTx.TryGet(it.Transaction.GetHash());
                    Guard.Assert(nextit != null);
                    txToRemove.Add(nextit);
                }
            }
            var setAllRemoves = new SetEntries();

            foreach (TxMempoolEntry item in txToRemove)
            {
                this.CalculateDescendants(item, setAllRemoves);
            }

            this.RemoveStaged(setAllRemoves, false);
        }

        /// <inheritdoc />
        public void RemoveStaged(SetEntries stage, bool updateDescendants)
        {
            //AssertLockHeld(cs);
            this.UpdateForRemoveFromMempool(stage, updateDescendants);
            foreach (TxMempoolEntry it in stage)
            {
                this.RemoveUnchecked(it);
            }
        }

        /// <inheritdoc />
        public int Expire(long time)
        {
            //LOCK(cs);
            var toremove = new SetEntries();
            foreach (TxMempoolEntry entry in this.MapTx.EntryTime)
            {
                if (!(entry.Time < time)) break;
                toremove.Add(entry);
            }

            var stage = new SetEntries();
            foreach (TxMempoolEntry removeit in toremove)
            {
                this.CalculateDescendants(removeit, stage);
            }
            this.RemoveStaged(stage, false);
            
            return stage.Count;
        }

        /// <summary>
        /// Removes entry from memory pool.
        /// </summary>
        /// <param name="entry">Entry to remove.</param>
        private void RemoveUnchecked(TxMempoolEntry entry)
        {
            uint256 hash = entry.TransactionHash;
            foreach (TxIn txin in entry.Transaction.Inputs)
            {
                this.MapNextTx.Remove(this.MapNextTx.FirstOrDefault(w => w.OutPoint == txin.PrevOut));
            }

            if (this.vTxHashes.Any())
            {
                this.vTxHashes.Remove(entry);

                //vTxHashes[it] = std::move(vTxHashes.back());
                //vTxHashes[it].second->vTxHashesIdx = it->vTxHashesIdx;
                //vTxHashes.pop_back();
                //if (vTxHashes.size() * 2 < vTxHashes.capacity())
                //  vTxHashes.shrink_to_fit();
            }
            //else
            //  vTxHashes.clear();

            this.totalTxSize -= entry.GetTxSize();
            this.cachedInnerUsage -= entry.DynamicMemoryUsage();
            this.cachedInnerUsage -= this.mapLinks[entry]?.Parents?.Sum(p => p.DynamicMemoryUsage()) ?? 0 + this.mapLinks[entry]?.Children?.Sum(p => p.DynamicMemoryUsage()) ?? 0;
            this.mapLinks.Remove(entry);
            this.MapTx.Remove(entry);
            this.nTransactionsUpdated++;
            this.MinerPolicyEstimator.RemoveTx(hash);
        }

        /// <inheritdoc />
        public void CalculateDescendants(TxMempoolEntry entry, SetEntries setDescendants)
        {
            var stage = new SetEntries();
            if (!setDescendants.Contains(entry))
            {
                stage.Add(entry);
            }
            // Traverse down the children of entry, only adding children that are not
            // accounted for in setDescendants already (because those children have either
            // already been walked, or will be walked in this iteration).
            while (stage.Any())
            {
                TxMempoolEntry it = stage.First();
                setDescendants.Add(it);
                stage.Remove(it);

                SetEntries setChildren = this.GetMemPoolChildren(it);
                foreach (TxMempoolEntry childiter in setChildren)
                {
                    if (!setDescendants.Contains(childiter))
                    {
                        stage.Add(childiter);
                    }
                }
            }
        }

        /// <summary>
        /// For each transaction being removed, update ancestors and any direct children.
        /// </summary>
        /// <param name="entriesToRemove">Memory pool entries to remove.</param>
        /// <param name="updateDescendants">If updateDescendants is true, then also update in-mempool descendants' ancestor state.</param>
        private void UpdateForRemoveFromMempool(SetEntries entriesToRemove, bool updateDescendants)
        {
            // For each entry, walk back all ancestors and decrement size associated with this
            // transaction
            long nNoLimit = long.MaxValue;

            if (updateDescendants)
            {
                // updateDescendants should be true whenever we're not recursively
                // removing a tx and all its descendants, eg when a transaction is
                // confirmed in a block.
                // Here we only update statistics and not data in mapLinks (which
                // we need to preserve until we're finished with all operations that
                // need to traverse the mempool).
                foreach (TxMempoolEntry removeIt in entriesToRemove)
                {
                    var setDescendants = new SetEntries();
                    this.CalculateDescendants(removeIt, setDescendants);
                    setDescendants.Remove(removeIt); // don't update state for self
                    long modifySize = -removeIt.GetTxSize();
                    long modifyFee = -removeIt.ModifiedFee;
                    long modifySigOps = -removeIt.SigOpCost;

                    foreach (TxMempoolEntry dit in setDescendants)
                        dit.UpdateAncestorState(modifySize, modifyFee, -1, modifySigOps);
                }
            }

            foreach (TxMempoolEntry entry in entriesToRemove)
            {
                var setAncestors = new SetEntries();
                string dummy = string.Empty;
                // Since this is a tx that is already in the mempool, we can call CMPA
                // with fSearchForParents = false.  If the mempool is in a consistent
                // state, then using true or false should both be correct, though false
                // should be a bit faster.
                // However, if we happen to be in the middle of processing a reorg, then
                // the mempool can be in an inconsistent state.  In this case, the set
                // of ancestors reachable via mapLinks will be the same as the set of
                // ancestors whose packages include this transaction, because when we
                // add a new transaction to the mempool in addUnchecked(), we assume it
                // has no children, and in the case of a reorg where that assumption is
                // false, the in-mempool children aren't linked to the in-block tx's
                // until UpdateTransactionsFromBlock() is called.
                // So if we're being called during a reorg, ie before
                // UpdateTransactionsFromBlock() has been called, then mapLinks[] will
                // differ from the set of mempool parents we'd calculate by searching,
                // and it's important that we use the mapLinks[] notion of ancestor
                // transactions as the set of things to update for removal.
                this.CalculateMemPoolAncestors(entry, setAncestors, nNoLimit, nNoLimit, nNoLimit, nNoLimit, out dummy, false);
                // Note that UpdateAncestorsOf severs the child links that point to
                // removeIt in the entries for the parents of removeIt.
                this.UpdateAncestorsOf(false, entry, setAncestors);
            }

            // After updating all the ancestor sizes, we can now sever the link between each
            // transaction being removed and any mempool children (ie, update setMemPoolParents
            // for each direct child of a transaction being removed).
            foreach (TxMempoolEntry removeIt in entriesToRemove)
            {
                this.UpdateChildrenForRemoval(removeIt);
            }
        }

        /// <summary>
        /// Sever link between specified transaction and direct children.
        /// </summary>
        /// <param name="entry">Memory pool entry.</param>
        private void UpdateChildrenForRemoval(TxMempoolEntry entry)
        {
            SetEntries setMemPoolChildren = this.GetMemPoolChildren(entry);
            foreach (TxMempoolEntry updateIt in setMemPoolChildren)
                this.UpdateParent(updateIt, entry, false);
        }

        /// <inheritdoc />
        public void RemoveForBlock(IEnumerable<Transaction> vtx, int blockHeight)
        {
            var entries = new List<TxMempoolEntry>();
            foreach (Transaction tx in vtx)
            {
                uint256 hash = tx.GetHash();
                TxMempoolEntry entry = this.MapTx.TryGet(hash);
                if (entry != null)
                    entries.Add(entry);
            }

            // Before the txs in the new block have been removed from the mempool, update policy estimates
            this.MinerPolicyEstimator.ProcessBlock(blockHeight, entries);
            foreach (Transaction tx in vtx)
            {
                uint256 hash = tx.GetHash();

                TxMempoolEntry entry = this.MapTx.TryGet(hash);
                if (entry != null)
                {
                    var stage = new SetEntries();
                    stage.Add(entry);
                    this.RemoveStaged(stage, true);
                }

                this.RemoveConflicts(tx);
                this.ClearPrioritisation(tx.GetHash());
            }
            this.lastRollingFeeUpdate = this.TimeProvider.GetTime();
            this.blockSinceLastRollingFeeBump = true;
        }

        /// <summary>
        /// Removes conflicting transactions.
        /// </summary>
        /// <param name="tx">Transaction to remove conflicts from.</param>
        private void RemoveConflicts(Transaction tx)
        {
            // Remove transactions which depend on inputs of tx, recursively
            //LOCK(cs);
            foreach (TxIn txInput in tx.Inputs)
            {
                NextTxPair it = this.MapNextTx.FirstOrDefault(p => p.OutPoint == txInput.PrevOut);
                if (it != null)
                {
                    Transaction txConflict = it.Transaction;
                    if (txConflict != tx)
                    {
                        this.ClearPrioritisation(txConflict.GetHash());
                        this.RemoveRecursive(txConflict);
                    }
                }
            }
        }

        /// <summary>
        /// Clears the prioritisation for a transaction.
        /// </summary>
        /// <param name="hash">Transaction hash.</param>
        private void ClearPrioritisation(uint256 hash)
        { 
            //LOCK(cs);
            this.mapDeltas.Remove(hash);
        }

        /// <summary>
        /// Get the amount of dynamic memory being used by the memory pool.
        /// </summary>
        /// <returns>Number of bytes in use by memory pool.</returns>
        public long DynamicMemoryUsage()
        {
            // TODO : calculate roughly the size of each element in its list

            //LOCK(cs);
            // Estimate the overhead of mapTx to be 15 pointers + an allocation, as no exact formula for boost::multi_index_contained is implemented.
            //int sizeofEntry = 10;
            //int sizeofDelta = 10;
            //int sizeofLinks = 10;
            //int sizeofNextTx = 10;
            //int sizeofHashes = 10;

            //return sizeofEntry*this.MapTx.Count +
            //       sizeofNextTx*this.mapNextTx.Count +
            //       sizeofDelta*this.mapDeltas.Count +
            //       sizeofLinks*this.mapLinks.Count +
            //       sizeofHashes*this.vTxHashes.Count +
            //       cachedInnerUsage;

            long returnVal = this.MapTx.Values.Sum(m => m.DynamicMemoryUsage()) + this.cachedInnerUsage;
            return returnVal;
        }

        /// <inheritdoc />
        public void TrimToSize(long sizelimit, List<uint256> pvNoSpendsRemaining = null)
        {
            //LOCK(cs);

            int nTxnRemoved = 0;
            var maxFeeRateRemoved = new FeeRate(0);
            while (this.MapTx.Any() && this.DynamicMemoryUsage() > sizelimit)
            {
                TxMempoolEntry it = this.MapTx.DescendantScore.First();

                // We set the new mempool min fee to the feerate of the removed set, plus the
                // "minimum reasonable fee rate" (ie some value under which we consider txn
                // to have 0 fee). This way, we don't allow txn to enter mempool with feerate
                // equal to txn which were removed with no block in between.
                var removed = new FeeRate(it.ModFeesWithDescendants, (int)it.SizeWithDescendants);
                removed = new FeeRate(new Money(removed.FeePerK + this.minReasonableRelayFee.FeePerK));

                this.trackPackageRemoved(removed);
                maxFeeRateRemoved = new FeeRate(Math.Max(maxFeeRateRemoved.FeePerK, removed.FeePerK));

                var stage = new SetEntries();
                this.CalculateDescendants(it, stage);
                nTxnRemoved += stage.Count;

                var txn = new List<Transaction>();
                if (pvNoSpendsRemaining != null)
                {
                    foreach (TxMempoolEntry setEntry in stage)
                        txn.Add(setEntry.Transaction);
                }

                this.RemoveStaged(stage, false);
                if (pvNoSpendsRemaining != null)
                {
                    foreach (Transaction tx in txn)
                    {
                        foreach (TxIn txin in tx.Inputs)
                        {
                            if (this.Exists(txin.PrevOut.Hash))
                                continue;
                            NextTxPair iter = this.MapNextTx.FirstOrDefault(p => p.OutPoint == new OutPoint(txin.PrevOut.Hash, 0));
                            if (iter == null || iter.OutPoint.Hash != txin.PrevOut.Hash)
                                pvNoSpendsRemaining.Add(txin.PrevOut.Hash);
                        }
                    }
                }
            }

            if (maxFeeRateRemoved > new FeeRate(0))
                this.logger.LogInformation($"Removed {nTxnRemoved} txn, rolling minimum fee bumped to {maxFeeRateRemoved}");
        }

        /// <inheritdoc />
        public FeeRate GetMinFee(long sizelimit)
        {
            //LOCK(cs);
            if (!this.blockSinceLastRollingFeeBump || this.rollingMinimumFeeRate == 0)
            {
                this.logger.LogTrace("(-)[ROLLING_MIN]:{0}", this.rollingMinimumFeeRate);
                return new FeeRate(new Money((int)this.rollingMinimumFeeRate));
            }

            long time = this.TimeProvider.GetTime();
            if (time > this.lastRollingFeeUpdate + 10)
            {
                double halflife = RollingFeeHalflife;
                if (this.DynamicMemoryUsage() < sizelimit / 4)
                    halflife /= 4;
                else if (this.DynamicMemoryUsage() < sizelimit / 2)
                    halflife /= 2;

                this.rollingMinimumFeeRate = this.rollingMinimumFeeRate / Math.Pow(2.0, (time - this.lastRollingFeeUpdate) / halflife);
                this.lastRollingFeeUpdate = time;

                if (this.rollingMinimumFeeRate < (double)this.minReasonableRelayFee.FeePerK.Satoshi / 2)
                {
                    this.rollingMinimumFeeRate = 0;
                    this.logger.LogTrace("(-)[LESS_THAN_MINREASONABLE]:0");
                    return new FeeRate(0);
                }
            }

            double ret = Math.Max(this.rollingMinimumFeeRate, this.minReasonableRelayFee.FeePerK.Satoshi);

            return new FeeRate(new Money((int)ret));
        }

        /// <inheritdoc />
        public void ApplyDeltas(uint256 hash, ref double dPriorityDelta, ref Money nFeeDelta)
        {
            //LOCK(cs);
            DeltaPair delta = this.mapDeltas.TryGet(hash);
            if (delta == null)
                return;

            dPriorityDelta += delta.Delta;
            nFeeDelta += delta.Amount;
        }

        /// <inheritdoc />
        public static double AllowFreeThreshold()
        {
            return Money.COIN * 144 / 250;
        }

        /// <inheritdoc />
        public static bool AllowFree(double dPriority)
        {
            // Large (in bytes) low-priority (new, small-coin) transactions
            // need a fee.
            return dPriority > AllowFreeThreshold();
        }

        /// <inheritdoc />
        public void WriteFeeEstimates(BitcoinStream stream)
        {
        }

        /// <inheritdoc />
        public void ReadFeeEstimates(BitcoinStream stream)
        {
        }

        /// <inheritdoc />
        public int GetTransactionsUpdated()
        {
            return this.nTransactionsUpdated;
        }

        /// <inheritdoc />
        public void AddTransactionsUpdated(int n)
        {
            this.nTransactionsUpdated += n;
        }

        /// <summary>
        /// Indexed transaction set used to store memory pool transactions.
        /// </summary>
        public class IndexedTransactionSet : Dictionary<uint256, TxMempoolEntry>
        {
            /// <summary>
            /// Constructs a indexed transaction set.
            /// </summary>
            public IndexedTransactionSet() : base(new SaltedTxidHasher())
            {
            }

            /// <summary>Gets a collection of memory pool entries ordered by descendant score.</summary>
            public IEnumerable<TxMempoolEntry> DescendantScore
            {
                get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByDescendantScore()); }
            }

            /// <summary>Gets a collection of memory pool entries ordered by entry time.</summary>
            public IEnumerable<TxMempoolEntry> EntryTime
            {
                get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByEntryTime()); }
            }

            /// <summary>Gets a collection of memory pool entries ordered by mining score.</summary>
            public IEnumerable<TxMempoolEntry> MiningScore
            {
                get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByScore()); }
            }

            /// <summary>Gets a collection of memory pool entries ordered by ancestor score.</summary>
            public IEnumerable<TxMempoolEntry> AncestorScore
            {
                get { return this.Values.OrderBy(o => o, new CompareTxMemPoolEntryByAncestorFee()); }
            }

            /// <summary>
            /// Adds an entry to the transaction set.
            /// </summary>
            /// <param name="entry">Entry to add.</param>
            public void Add(TxMempoolEntry entry)
            {
                this.Add(entry.TransactionHash, entry);
            }

            /// <summary>
            /// Removes an entry from the transaction set.
            /// </summary>
            /// <param name="entry">Transaction to remove.</param>
            public void Remove(TxMempoolEntry entry)
            {
                this.Remove(entry.TransactionHash);
            }

            /// <summary>
            /// Salted transaction id hasher for comparing transaction hash codes.
            /// </summary>
            private class SaltedTxidHasher : IEqualityComparer<uint256>
            {
                /// <summary>
                /// Whether two transaction hashes are equal.
                /// </summary>
                /// <param name="x">First hash.</param>
                /// <param name="y">Second hash.</param>
                /// <returns>Whether the hashes are equal.</returns>
                public bool Equals(uint256 x, uint256 y)
                {
                    return x == y;
                }

                /// <summary>
                /// Gets the hash code for the transaction hash.
                /// </summary>
                /// <param name="obj">Transaction hash.</param>
                /// <returns></returns>
                public int GetHashCode(uint256 obj)
                {
                    // todo: need to compare with the c++ implementation
                    return obj.GetHashCode();
                }
            }

            /// <summary>
            /// Sort an entry by max(score/size of entry's tx, score/size with all descendants).
            /// </summary>
            private class CompareTxMemPoolEntryByDescendantScore : IComparer<TxMempoolEntry>
            {
                /// <inheritdoc />
                public int Compare(TxMempoolEntry a, TxMempoolEntry b)
                {
                    bool fUseADescendants = this.UseDescendantScore(a);
                    bool fUseBDescendants = this.UseDescendantScore(b);

                    double aModFee = fUseADescendants ? a.ModFeesWithDescendants.Satoshi : a.ModifiedFee;
                    double aSize = fUseADescendants ? a.SizeWithDescendants : a.GetTxSize();

                    double bModFee = fUseBDescendants ? b.ModFeesWithDescendants.Satoshi : b.ModifiedFee;
                    double bSize = fUseBDescendants ? b.SizeWithDescendants : b.GetTxSize();

                    // Avoid division by rewriting (a/b > c/d) as (a*d > c*b).
                    double f1 = aModFee * bSize;
                    double f2 = aSize * bModFee;

                    if (f1 == f2)
                    {
                        if (a.Time >= b.Time)
                            return -1;
                        return 1;
                    }

                    if (f1 <= f2)
                        return -1;
                    return 1;
                }

                /// <summary>
                /// Calculate which score to use for an entry (avoiding division).
                /// </summary>
                /// <param name="a">Memory pool entry.</param>
                /// <returns>Whether to use descendant score.</returns>
                private bool UseDescendantScore(TxMempoolEntry a)
                {
                    double f1 = (double)a.ModifiedFee * a.SizeWithDescendants;
                    double f2 = (double)a.ModFeesWithDescendants.Satoshi * a.GetTxSize();
                    return f2 > f1;
                }
            }

            /// <summary>
            /// Sort by entry time.
            /// </summary>
            private class CompareTxMemPoolEntryByEntryTime : IComparer<TxMempoolEntry>
            {
                /// <inheritdoc />
                public int Compare(TxMempoolEntry a, TxMempoolEntry b)
                {
                    if (a.Time < b.Time)
                        return -1;
                    return 1;
                }
            }

            /// <summary>
            /// Sort by score of entry ((fee+delta)/size) in descending order.
            /// </summary>
            private class CompareTxMemPoolEntryByScore : IComparer<TxMempoolEntry>
            {
                /// <inheritdoc />
                public int Compare(TxMempoolEntry a, TxMempoolEntry b)
                {
                    double f1 = (double)a.ModifiedFee * b.GetTxSize();
                    double f2 = (double)b.ModifiedFee * a.GetTxSize();
                    if (f1 == f2)
                    {
                        if (a.TransactionHash < b.TransactionHash)
                            return 1;
                        return -1;
                    }
                    if (f1 > f2)
                        return -1;
                    return 1;
                }
            }

            /// <summary>
            /// Sort by ancestor fee.
            /// </summary>
            private class CompareTxMemPoolEntryByAncestorFee : IComparer<TxMempoolEntry>
            {
                /// <inheritdoc />
                public int Compare(TxMempoolEntry a, TxMempoolEntry b)
                {
                    return TxMempoolEntry.CompareFees(a, b);
                }
            }
        }

        /// <summary>
        /// Sort by transaction hash.
        /// </summary>
        public class CompareIteratorByHash : IComparer<TxMempoolEntry>
        {
            /// <inheritdoc />
            public int Compare(TxMempoolEntry a, TxMempoolEntry b)
            {
                return a.CompareTo(b);
            }
        }

        /// <summary>
        /// Transaction links to parent and child sets for a given transaction.
        /// </summary>
        public class TxLinks
        {
            /// <summary>Parent memory pool entries</summary>
            public SetEntries Parents;

            /// <summary>Child memory pool entries</summary>
            public SetEntries Children;
        }

;

        /// <summary>
        /// Set of memory pool entries.
        /// </summary>
        public class SetEntries : SortedSet<TxMempoolEntry>, IEquatable<SetEntries>, IEqualityComparer<TxMempoolEntry>
        {
            /// <summary>
            /// Constructs a set of memory pool entries.
            /// </summary>
            public SetEntries() : base(new CompareIteratorByHash())
            {
            }

            /// <inheritdoc />
            public bool Equals(SetEntries other)
            {
                return this.SequenceEqual(other, this);
            }

            /// <inheritdoc />
            public bool Equals(TxMempoolEntry x, TxMempoolEntry y)
            {
                return x.TransactionHash == y.TransactionHash;
            }

            /// <summary>
            /// Gets the hash code for a memory pool entry.
            /// </summary>
            /// <param name="obj">Memory pool entry.</param>
            /// <returns>Hash code.</returns>
            public int GetHashCode(TxMempoolEntry obj)
            {
                return obj?.TransactionHash?.GetHashCode() ?? 0;
            }
        }

        /// <summary>
        /// Sorted list of transaction parent/child links.
        /// </summary>
        public class TxlinksMap : SortedList<TxMempoolEntry, TxLinks>
        {
            /// <summary>
            /// Constructs a new transaction links collection.
            /// </summary>
            public TxlinksMap() : base(new CompareIteratorByHash())
            {
            }
        }

        /// <summary>
        /// A pair of delta, amount pairs.
        /// </summary>
        public class DeltaPair
        {
            /// <summary>The value of the delta.</summary>
            public double Delta;

            /// <summary>The amount.</summary>
            public Money Amount;
        }

        /// <summary>
        /// Next transaction pair.
        /// </summary>
        public class NextTxPair
        {
            /// <summary>The outpoint of the transaction.</summary>
            public OutPoint OutPoint;

            /// <summary>The next transaction.</summary>
            public Transaction Transaction;
        }
    }
}
