using System;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Track the height and time at which transaction was final.
    /// </summary>
    /// <remarks>
    /// Will be set to the blockchain height and median time past
    /// values that would be necessary to satisfy all relative locktime
    /// raints (BIP68) of this tx given our view of block chain history.
    /// </remarks>
    public class LockPoints // todo: replace with SequenceLock
    {
        /// <summary>Block chain height.</summary>
        public int Height;

        /// <summary>Median time past values.</summary>
        public long Time;

        /// <summary>
        /// The block with the highest height of all the blocks which have sequence locked prevouts.
        /// </summary>
        /// <remarks>
        /// As long as the current chain descends from the highest height block
        /// containing one of the inputs used in the calculation, then the cached
        /// values are still valid even after a reorg.
        /// </remarks>
        public ChainedHeader MaxInputBlock;
    }

    /// <summary>
    /// This interface includes the fields required for fee comparison.
    /// </summary>
    public interface ITxMempoolFees
    {
        Money ModFeesWithAncestors { get; }
        long SizeWithAncestors { get; }
    }

    /// <summary>
    /// A transaction entry in the memory pool.
    /// </summary>
    public class TxMempoolEntry:IComparable, ITxMempoolFees
    {
        /// <summary>Index in memory pools vTxHashes.</summary>
        public volatile uint vTxHashesIdx;

        /// <summary>The modified size of the transaction used for priority.</summary>
        private long nModSize;

        /// <summary>The total memory usage.</summary>
        private long nUsageSize;

        /// <summary>Priority when entering the memory pool.</summary>
        private double entryPriority;

        /// <summary>
        /// Constructs a transaction memory pool entry.
        /// </summary>
        /// <param name="transaction">Transaction for the entry.</param>
        /// <param name="nFee">Fee for the transaction in the entry in the memory pool.</param>
        /// <param name="nTime">The local time when entering the memory pool.</param>
        /// <param name="entryPriority">Priority when entering the memory pool.</param>
        /// <param name="entryHeight">The chain height when entering the mempool.</param>
        /// <param name="inChainInputValue">The sum of all txin values that are already in blockchain.</param>
        /// <param name="spendsCoinbase">Whether the transaction spends a coinbase.</param>
        /// <param name="nSigOpsCost">The total signature operations cost.</param>
        /// <param name="lp">Tthe lock points that track the height and time at which tx was final.</param>
        /// <param name="consensusOptions">Proof of work consensus options used to compute transaction weight and modified size.</param>
        public TxMempoolEntry(Transaction transaction, Money nFee,
            long nTime, double entryPriority, int entryHeight,
            Money inChainInputValue, bool spendsCoinbase,
            long nSigOpsCost, LockPoints lp, ConsensusOptions consensusOptions)
        {
            this.Transaction = transaction;
            this.TransactionHash = transaction.GetHash();
            this.Fee = nFee;
            this.Time = nTime;
            this.entryPriority = entryPriority;
            this.EntryHeight = entryHeight;
            this.InChainInputValue = inChainInputValue;
            this.SpendsCoinbase = spendsCoinbase;
            this.SigOpCost = nSigOpsCost;
            this.LockPoints = lp;

            this.TxWeight = MempoolValidator.GetTransactionWeight(transaction, consensusOptions);
            this.nModSize = MempoolValidator.CalculateModifiedSize(this.Transaction.GetSerializedSize(), this.Transaction, consensusOptions);

            this.nUsageSize = transaction.GetSerializedSize(); // RecursiveDynamicUsage(*tx) + memusage::DynamicUsage(Transaction);

            this.CountWithDescendants = 1;
            this.SizeWithDescendants = this.GetTxSize();
            this.ModFeesWithDescendants = this.Fee;
            Money nValueIn = transaction.TotalOut + this.Fee;
            Guard.Assert(this.InChainInputValue <= nValueIn);

            this.feeDelta = 0;

            this.CountWithAncestors = 1;
            this.SizeWithAncestors = this.GetTxSize();
            this.ModFeesWithAncestors = this.Fee;
            this.SigOpCostWithAncestors = this.SigOpCost;
        }

        /// <summary>
        /// Copy constructor for a transaction memory pool entry.
        /// </summary>
        /// <param name="other">Entry to copy.</param>
        /// <exception cref="NotImplementedException"/>
        public TxMempoolEntry(TxMempoolEntry other)
        {
            throw new NotImplementedException();
        }

        /// <summary>Gets the transaction from the entry in the memory pool.</summary>
        public Transaction Transaction { get; private set; }

        /// <summary>Gets the hash of the transaction in the entry in the memory pool.</summary>
        public uint256 TransactionHash { get; private set; }

        /// <summary>Gets the fee for the transaction in the entry in the memory pool.</summary>
        /// <remarks>Cached to avoid expensive parent-transaction lookups.</remarks>
        public Money Fee { get; private set; }

        /// <summary>
        /// Gets the transaction weight of the transaction in the entry in the memory pool.
        /// </summary>
        /// <remarks>
        /// Cached to avoid recomputing transaction weight.
        /// Also used for GetTxSize().
        /// </remarks>
        public long TxWeight { get; private set; }

        /// <summary>Gets the local time when entering the memory pool.</summary>
        public long Time { get; private set; }

        /// <summary>Gets the chain height when entering the mempool.</summary>
        public int EntryHeight { get; private set; }

        /// <summary>Gets the sum of all txin values that are already in blockchain.</summary>
        public Money InChainInputValue { get; private set; }

        /// <summary>Gets whether the transaction spends a coinbase.</summary>
        public bool SpendsCoinbase { get; private set; }

        /// <summary>Gets the total signature operations cost.</summary>
        public long SigOpCost { get; private set; }

        /// <summary>Gets the lock points that track the height and time at which tx was final.</summary>
        public LockPoints LockPoints { get; private set; }

        // Information about descendants of this transaction that are in the
        // mempool; if we remove this transaction we must remove all of these
        // descendants as well.  if <see cref="CountWithDescendants"/> is 0, treat this entry as
        // dirty, and <see cref="SizeWithDescendants"/> and <see cref="ModFeesWithDescendants"/> will not be
        // correct.

        /// <summary>Gets the number of descendant transactions.</summary>
        public long CountWithDescendants { get; private set; }

        /// <summary>Gets the size of the transaction with it's decendants.</summary>
        public long SizeWithDescendants { get; private set; }

        /// <summary>Gets the total fees of transaction including it's decendants.</summary>
        public Money ModFeesWithDescendants { get; private set; }

        // Analogous statistics for ancestor transactions

        /// <summary>Gets the number of ancestor transactions.</summary>
        public long CountWithAncestors { get; private set; }

        /// <summary>
        /// Gets the size of the transaction with it's ancestors.</summary>
        public long SizeWithAncestors { get; private set; }

        /// <summary>Gets the total fees of the transaction including it's ancestors.</summary>
        public Money ModFeesWithAncestors { get; private set; }

        /// <summary>Gets the total cost of the signature operations for the transaction including it's ancestors.</summary>
        public long SigOpCostWithAncestors { get; private set; }

        /// <summary>Gets the modified fee which is the sum of the transaction <see cref="Fee"/> and the <see cref="feeDelta"/>.</summary>
        public long ModifiedFee => this.Fee + this.feeDelta;

        /// <summary>
        /// Gets the difference between transactions fees.
        /// </summary>
        /// <remarks>
        /// Used for determining the priority of the transaction for mining in a block.
        /// </remarks>
        internal long feeDelta { get; private set; }

        /// <summary>
        /// Gets the priority of the memory pool entry given the current chain height.
        /// </summary>
        /// <param name="currentHeight">Current chain height.</param>
        /// <returns>Transaction priority.</returns>
        /// <remarks>
        /// Fast calculation of lower bound of current priority as update
        /// from entry priority. Only inputs that were originally in-chain will age.
        /// </remarks>
        public double GetPriority(int currentHeight)
        {
            double deltaPriority = ((double)(currentHeight - this.EntryHeight) * this.InChainInputValue.Satoshi) / this.nModSize;
            double dResult = this.entryPriority + deltaPriority;
            if (dResult < 0) // This should only happen if it was called with a height below entry height
                dResult = 0;
            return dResult;
        }

        /// <summary>
        /// Gets the transaction size. See <see cref="Transaction.GetVirtualSize"/>.
        /// </summary>
        /// <returns>The transaction size.</returns>
        public long GetTxSize()
        {
            return (long)this.Transaction.GetVirtualSize();
        }

        /// <summary>
        /// Gets the dynamic memory usage in bytes.
        /// </summary>
        /// <returns>The dynamic memory usage value.</returns>
        public long DynamicMemoryUsage()
        {
            return this.nUsageSize;
        }

        /// <summary>
        /// Adjusts the descendant state, if this entry is not dirty.
        /// </summary>
        /// <param name="modifySize">Amount to add to the decendant size of this entry.</param>
        /// <param name="modifyFee">Amount to add to the total of the decendants modify fees for this entry.</param>
        /// <param name="modifyCount">Count of transactions to add to the total count of descendant transactions for this entry.</param>
        public void UpdateDescendantState(long modifySize, Money modifyFee, long modifyCount)
        {
            this.SizeWithDescendants += modifySize;
            Guard.Assert(this.SizeWithDescendants > 0);
            this.ModFeesWithDescendants += modifyFee;
            this.CountWithDescendants += modifyCount;
            Guard.Assert(this.CountWithDescendants > 0);
        }

        /// <summary>
        /// Adjusts the ancestor state.
        /// </summary>
        /// <param name="modifySize">Amount to add to the ancestor size of this entry.</param>
        /// <param name="modifyFee">Amount to add to the total of the ancestor modify fees for this entry.</param>
        /// <param name="modifyCount">Count of transactions to add to the total count of ancestor transactions for this entry.</param>
        /// <param name="modifySigOps">Cost to add to the total signature operators cost for all ancestor transactions for this entry.</param>
        public void UpdateAncestorState(long modifySize, Money modifyFee, long modifyCount, long modifySigOps)
        {
            this.SizeWithAncestors += modifySize;
            Guard.Assert(this.SizeWithAncestors > 0);
            this.ModFeesWithAncestors += modifyFee;
            this.CountWithAncestors += modifyCount;
            Guard.Assert(this.CountWithAncestors > 0);
            this.SigOpCostWithAncestors += modifySigOps;
            Guard.Assert(this.SigOpCostWithAncestors >= 0);
        }

        /// <summary>
        /// Updates the fee delta used for mining priority score, and the
        /// modified fees with descendants.
        /// </summary>
        /// <param name="newFeeDelta">New fee delta to use.</param>
        public void UpdateFeeDelta(long newFeeDelta)
        {
            this.ModFeesWithDescendants += newFeeDelta - this.feeDelta;
            this.ModFeesWithAncestors += newFeeDelta - this.feeDelta;
            this.feeDelta = newFeeDelta;
        }

        /// <summary>
        /// Update the <see cref="LockPoints"/> after a reorg.
        /// </summary>
        /// <param name="lp">New lockpoints.</param>
        public void UpdateLockPoints(LockPoints lp)
        {
            this.LockPoints = lp;
        }

        /// <summary>
        /// String representation of the memory pool entry.
        /// Prepends the transaction hash for this entry to the string.
        /// </summary>
        /// <returns>The string representation of the memory pool entry.</returns>
        public override string ToString()
        {
            return $"{this.TransactionHash} - {base.ToString()}";
        }

        /// <summary>
        /// Default comparator for comparing this object to another TxMemPoolEntry object.
        /// </summary>
        /// <param name="other">Memory pool entry to compare to.</param>
        /// <returns>Result of comparison function.</returns>
        public int CompareTo(object other)
        {
            return uint256.Comparison(this.TransactionHash, (other as TxMempoolEntry).TransactionHash);
        }

        /// <summary>
        /// Used to compare fees on objects supporting the IComparable and ITxMempoolFees interfaces.
        /// </summary>
        /// <typeparam name="T">A type that supports the IComparable and ITxMempoolFees interfaces.</typeparam>
        /// <param name="a">The first object to compare.</param>
        /// <param name="b">The second object to compare.</param>
        /// <returns>Returns -1 if a less than b, 0 if a equals b, and 1 if a greater than b.</returns>
        public static int CompareFees<T>(T a, T b) where T:IComparable,ITxMempoolFees
        {
            // Avoid division by rewriting (a/b > c/d) as (a*d > c*b).
            Money f1 = a.ModFeesWithAncestors * b.SizeWithAncestors;
            Money f2 = b.ModFeesWithAncestors * a.SizeWithAncestors;

            if (f1 == f2)
                return a.CompareTo(b);

            return (f1 < f2) ? 1 : -1;
        }
    }
}
