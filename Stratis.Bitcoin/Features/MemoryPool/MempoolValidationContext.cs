using NBitcoin;
using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class MempoolValidationState
    {
        public MempoolValidationState(bool limitFree) : this(limitFree, false, Money.Zero)
        {
        }

        public MempoolValidationState(bool limitFree, bool overrideMempoolLimit, Money absurdFee)
        {
            this.LimitFree = limitFree;
            this.AbsurdFee = absurdFee;
            this.OverrideMempoolLimit = overrideMempoolLimit;
        }

        public MempoolError Error { get; set; }

        public string ErrorMessage { get; set; }

        public MempoolValidationState Invalid(MempoolError error)
        {
            this.Error = error;
            this.IsInvalid = true;
            return this;
        }

        public MempoolValidationState Invalid(MempoolError error, string errorMessage)
        {
            this.Error = error;
            this.IsInvalid = true;
            this.ErrorMessage = errorMessage;
            return this;
        }

        public MempoolValidationState Fail(MempoolError error)
        {
            this.Error = error;
            return this;
        }

        public MempoolValidationState Fail(MempoolError error, string errorMessage)
        {
            this.Error = error;
            this.ErrorMessage = errorMessage;
            return this;
        }

        public Money AbsurdFee { get; set; }

        public bool MissingInputs { get; set; }

        public bool CorruptionPossible { get; set; }
        public bool IsInvalid { get; set; }

        public bool OverrideMempoolLimit { get; set; }

        public long AcceptTime { get; set; }

        public bool LimitFree { get; set; }

        // variables helpful for logging
        public long MempoolSize { get; set; }
        public long MempoolDynamicSize { get; set; }

        public void Throw()
        {
            throw new MempoolErrorException(this);
        }

        public override string ToString()
        {
            return $"{this.Error?.RejectCode}{this.ErrorMessage} (code {this.Error?.Code})";
        }
    }

    /// <summary>
    /// A context to hold validation data when adding
    /// a transaction to the memory pool.
    /// </summary>
    public class MempoolValidationContext
    {
        public MempoolValidationState State { get; }

        public List<uint256> SetConflicts { get; set; }

        public Transaction Transaction { get; }

        public uint256 TransactionHash { get; }

        public TxMempoolEntry Entry { get; set; }

        public MempoolCoinView View { get; set; }

        public int EntrySize { get; set; }

        public TxMempool.SetEntries AllConflicting { get; set; }

        public TxMempool.SetEntries SetAncestors { get; set; }

        public LockPoints LockPoints { get; set; }

        public Money ConflictingFees { get; set; }
        public long ConflictingSize { get; set; }
        public long ConflictingCount { get; set; }

        public Money ValueOut { get; set; }
        public Money Fees { get; set; }
        public Money ModifiedFees { get; set; }
        public long SigOpsCost { get; set; }

        public MempoolValidationContext(Transaction transaction, MempoolValidationState state)
        {
            this.Transaction = transaction;
            this.TransactionHash = transaction.GetHash();
            this.State = state;
        }
    }
}
