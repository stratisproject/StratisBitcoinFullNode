using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;
using System;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class LockPoints // todo: replace with SequenceLock 
    {
        // Will be set to the blockchain height and median time past
        // values that would be necessary to satisfy all relative locktime
        // raints (BIP68) of this tx given our view of block chain history
        public int Height;
        public long Time;
        // As long as the current chain descends from the highest height block
        // containing one of the inputs used in the calculation, then the cached
        // values are still valid even after a reorg.
        public ChainedBlock MaxInputBlock;
    };

    public class TxMempoolEntry
    {
        public Transaction Transaction { get; private set; }
        public uint256 TransactionHash { get; private set; }

        public Money Fee { get; private set; } //!< Cached to avoid expensive parent-transaction lookups
        public long TxWeight { get; private set; } //!< ... and avoid recomputing tx weight (also used for GetTxSize())
        private long nModSize; //!< ... and modified size for priority
        private long nUsageSize; //!< ... and total memory usage
        public long Time { get; private set; } //!< Local time when entering the mempool
        private double entryPriority; //!< Priority when entering the mempool
        public int EntryHeight { get; private set; } //!< Chain height when entering the mempool
        public Money InChainInputValue { get; private set; } //!< Sum of all txin values that are already in blockchain
        public bool SpendsCoinbase { get; private set; } //!< keep track of transactions that spend a coinbase
        public long SigOpCost { get; private set; } //!< Total sigop cost
        internal long feeDelta { get; private set; } //!< Used for determining the priority of the transaction for mining in a block
        public LockPoints LockPoints { get; private set; } //!< Track the height and time at which tx was final

        // Information about descendants of this transaction that are in the
        // mempool; if we remove this transaction we must remove all of these
        // descendants as well.  if nCountWithDescendants is 0, treat this entry as
        // dirty, and nSizeWithDescendants and nModFeesWithDescendants will not be
        // correct.
        public long CountWithDescendants { get; private set; } //!< number of descendant transactions
        public long SizeWithDescendants { get; private set; } //!< ... and size
        public Money ModFeesWithDescendants { get; private set; } //!< ... and total fees (all including us)

        // Analogous statistics for ancestor transactions
        public long CountWithAncestors { get; private set; }
        public long SizeWithAncestors { get; private set; }
        public Money ModFeesWithAncestors { get; private set; }
        public long SigOpCostWithAncestors { get; private set; }



        

        public TxMempoolEntry(Transaction transaction, Money nFee,
            long nTime, double entryPriority, int entryHeight,
            Money inChainInputValue, bool spendsCoinbase,
            long nSigOpsCost, LockPoints lp, PowConsensusOptions consensusOptions)
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

        public TxMempoolEntry(TxMempoolEntry other)
        {
            throw new NotImplementedException();
        }
        
         // Fast calculation of lower bound of current priority as update
         // from entry priority. Only inputs that were originally in-chain will age.
        public double GetPriority(int currentHeight)
        {
            double deltaPriority = ((double) (currentHeight - this.EntryHeight)* this.InChainInputValue.Satoshi)/ this.nModSize;
            double dResult = this.entryPriority + deltaPriority;
            if (dResult < 0) // This should only happen if it was called with a height below entry height
                dResult = 0;
            return dResult;
        }

        public long GetTxSize()
        {
            return (long) this.Transaction.GetVirtualSize();
        }

        public long ModifiedFee => this.Fee + this.feeDelta;

        public long DynamicMemoryUsage()
        {
            return this.nUsageSize;
        }

        // Adjusts the descendant state, if this entry is not dirty.
        public void UpdateDescendantState(long modifySize, Money modifyFee, long modifyCount)
        {
            this.SizeWithDescendants += modifySize;
            Guard.Assert(this.SizeWithDescendants > 0);
            this.ModFeesWithDescendants += modifyFee;
            this.CountWithDescendants += modifyCount;
            Guard.Assert(this.CountWithDescendants > 0);
        }

        // Adjusts the ancestor state
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

        // Updates the fee delta used for mining priority score, and the
        // modified fees with descendants.
        public void UpdateFeeDelta(long newFeeDelta)
        {
            this.ModFeesWithDescendants += newFeeDelta - this.feeDelta;
            this.ModFeesWithAncestors += newFeeDelta - this.feeDelta;
            this.feeDelta = newFeeDelta;
        }

        // Update the LockPoints after a reorg
        public void UpdateLockPoints(LockPoints lp)
        {
            this.LockPoints = lp;
        }

        public volatile uint vTxHashesIdx; //!< Index in mempool's vTxHashes

        public override string ToString()
        {
            return $"{this.TransactionHash} - {base.ToString()}";
        }
    }
}
