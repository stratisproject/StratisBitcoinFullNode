using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// A state used when validating a new transaction.
    /// A transaction must be validated before being added to the memory pool.
    /// </summary>
    public class MempoolValidationState
    {
        /// <summary>
        /// Constructs an instance of the memory pool validation state object.
        /// </summary>
        /// <param name="limitFree">Whether free transactions were limited.</param>
        public MempoolValidationState(bool limitFree) : this(limitFree, false, Money.Zero)
        {
        }

        /// <summary>
        /// Constructs and instance of the memory pool validation state object.
        /// </summary>
        /// <param name="limitFree">Whether free transactions were limited.</param>
        /// <param name="overrideMempoolLimit">Whether the memory pool limit was overridden.</param>
        /// <param name="absurdFee">The amount that was used for calculating an absurdly high fee.</param>
        public MempoolValidationState(bool limitFree, bool overrideMempoolLimit, Money absurdFee)
        {
            this.LimitFree = limitFree;
            this.AbsurdFee = absurdFee;
            this.OverrideMempoolLimit = overrideMempoolLimit;
        }

        /// <summary>Get or sets the current error status for memory pool.</summary>
        public MempoolError Error { get; set; }

        /// <summary>Gets or sets the current error message for memory pool.</summary>
        public string ErrorMessage { get; set; }

        /// <summary>Gets or sets the value for an absurdly high transaction fee.</summary>
        public Money AbsurdFee { get; set; }

        /// <summary>Gets or sets whether there are missing inputs on the transaction.</summary>
        public bool MissingInputs { get; set; }

        /// <summary>Gets or sets whether transaction pool could be in a corrupted state.</summary>
        public bool CorruptionPossible { get; set; }

        /// <summary>Gets or sets whether the validation state is in an invalid state.</summary>
        public bool IsInvalid { get; set; }

        /// <summary>Gets or sets whether the memory pool limit has been overridden.</summary>
        public bool OverrideMempoolLimit { get; set; }

        /// <summary>Gets or sets the acceptance time of the transaction to the memory pool.</summary>
        public long AcceptTime { get; set; }

        /// <summary>Gets or sets whether free transactions are being limited.</summary>
        public bool LimitFree { get; set; }

        // variables helpful for logging.

        /// <summary>Gets or sets the current number of transactions in the memory pool.</summary>
        public long MempoolSize { get; set; }

        /// <summary>Gets or sets the memory pools dynamic size in bytes.</summary>
        public long MempoolDynamicSize { get; set; }

        /// <summary>
        /// Sets the memory pool validation state to invalid.
        /// </summary>
        /// <param name="error">The current error.</param>
        /// <returns>The current validation state of the memory pool.</returns>
        public MempoolValidationState Invalid(MempoolError error)
        {
            this.Error = error;
            this.IsInvalid = true;
            return this;
        }

        /// <summary>
        /// Sets the memory pool validation state to invalid, with an error message.
        /// </summary>
        /// <param name="error">The current error.</param>
        /// <param name="errorMessage">The current error message.</param>
        /// <returns>The current validation state of the memory pool.</returns>
        public MempoolValidationState Invalid(MempoolError error, string errorMessage)
        {
            this.Error = error;
            this.IsInvalid = true;
            this.ErrorMessage = errorMessage;
            return this;
        }

        /// <summary>
        /// Sets the memory pool validation state to fail.
        /// </summary>
        /// <param name="error">The current error.</param>
        /// <returns>The current validation state of the memory pool.</returns>
        public MempoolValidationState Fail(MempoolError error)
        {
            this.Error = error;
            return this;
        }

        /// <summary>
        /// Sets the memory pool validation state to fail, with an error message.
        /// </summary>
        /// <param name="error">The current error.</param>
        /// <param name="errorMessage">The current error message.</param>
        /// <returns>The current validation state of the memory pool.</returns>
        public MempoolValidationState Fail(MempoolError error, string errorMessage)
        {
            this.Error = error;
            this.ErrorMessage = errorMessage;
            return this;
        }

        /// <summary>
        /// Throws a <see cref="MempoolErrorException"/> for the current error state.
        /// </summary>
        /// <exception cref="MempoolErrorException">Current error state.</exception>
        public void Throw()
        {
            throw new MempoolErrorException(this);
        }

        /// <summary>
        /// Gets a string formatted error message with code.
        /// </summary>
        /// <returns>The error message as a string.</returns>
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
        /// <summary>Gets the validation state of the memory pool.</summary>
        public MempoolValidationState State { get; }

        /// <summary>Gets or sets the collection of transaction set conflicts.</summary>
        public List<uint256> SetConflicts { get; set; }

        /// <summary>Gets the current transaction being validated.</summary>
        public Transaction Transaction { get; }

        /// <summary>Gets the hash of the current transaction being validated.</summary>
        public uint256 TransactionHash { get; }

        /// <summary>Gets or sets the current entry in the memory pool.</summary>
        public TxMempoolEntry Entry { get; set; }

        /// <summary>Gets or sets the current coin view of the transaction.</summary>
        public MempoolCoinView View { get; set; }

        /// <summary>Gets or sets the size of the current entry in the memory pool.</summary>
        public int EntrySize { get; set; }

        /// <summary>Gets or sets the set of all memory pool entries that are conflicting.</summary>
        public TxMempool.SetEntries AllConflicting { get; set; }

        /// <summary>Gets or sets the transaction set's ancestors.</summary>
        public TxMempool.SetEntries SetAncestors { get; set; }

        /// <summary>Gets or sets the lock points for the memory pool.</summary>
        public LockPoints LockPoints { get; set; }

        /// <summary>Gets or sets the conflicting fees for the memory pool.</summary>
        public Money ConflictingFees { get; set; }

        /// <summary>Gets or sets the memory pool entry size of the conflicting transaction.</summary>
        public long ConflictingSize { get; set; }

        /// <summary>Gets or sets the number of other entries that are conflicting transactions.</summary>
        public long ConflictingCount { get; set; }

        /// <summary>Value of the output of the transaction.</summary>
        public Money ValueOut { get; set; }

        /// <summary>Amount of the fees for the transaction.</summary>
        public Money Fees { get; set; }

        /// <summary>The Amount of the fees for the transaction after they have been modified.</summary>
        public Money ModifiedFees { get; set; }

        /// <summary>The total cost of the signature operations for the transaction.</summary>
        public long SigOpsCost { get; set; }

        /// <summary>
        /// Constructs a memory pool validation context object.
        /// </summary>
        /// <param name="transaction">The current transaction being validated.</param>
        /// <param name="state">The current memory pool validation state.</param>
        public MempoolValidationContext(Transaction transaction, MempoolValidationState state)
        {
            this.Transaction = transaction;
            this.TransactionHash = transaction.GetHash();
            this.State = state;
        }
    }
}
