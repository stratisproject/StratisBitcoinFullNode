using System;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    /// <summary>
    /// Exception thrown when memory pool exception occurs.
    /// </summary>
    public class MempoolErrorException : Exception
    {
        /// <summary>
        /// Constructs a memory pool exception object.
        /// Exception message is set from <see cref="MempoolValidationState.ErrorMessage"/>.
        /// </summary>
        /// <param name="state">Validation state of the memory pool.</param>
        public MempoolErrorException(MempoolValidationState state) : base(state.ErrorMessage)
        {
            this.ValidationState = state;
        }

        /// <summary>Gets the validation state of the memory pool.</summary>
        public MempoolValidationState ValidationState { get; private set; }
    }

    /// <summary>
    /// Memory pool error state.
    /// </summary>
    public class MempoolError
    {
        /// <summary>
        /// Default constructor for a memory pool error.
        /// </summary>
        public MempoolError()
        {
        }

        /// <summary>
        /// Constructor for memory pool error.
        /// </summary>
        /// <param name="rejectCode">Numeric reject code.</param>
        /// <param name="code">String representation of error code.</param>
        public MempoolError(int rejectCode, string code)
        {
            this.Code = code;
            this.RejectCode = rejectCode;
        }

        /// <summary>
        /// Constructor for memory pool error.
        /// </summary>
        /// <param name="consensusError">Error from consensus feature.</param>
        public MempoolError(ConsensusError consensusError)
        {
            this.ConsensusError = consensusError;
        }

        /// <summary>Gets or sets the string representation of the error code.</summary>
        public string Code { get; set; }

        /// <summary>Gets or sets the numeric representation of the error code.</summary>
        public int RejectCode { get; set; }

        /// <summary>Gets or sets the error from consensus feature.</summary>
        public ConsensusError ConsensusError { get; set; }
    }

    /// <summary>
    /// Predefined memory pool errors.
    /// </summary>
    public static class MempoolErrors
    {
        /// <summary>Public reject code for malformed transaction.</summary>
        public const int RejectMalformed = 0x01;

        /// <summary>Public reject code for Invalid transaction</summary>
        public const int RejectInvalid = 0x10;

        /// <summary>Public reject code for Obsolete Transaction.</summary>
        public const int RejectObsolete = 0x11;

        /// <summary>Public reject code for Duplicate Transaction.</summary>
        public const int RejectDuplicate = 0x12;

        /// <summary>Public reject code for Non standard transaction.</summary>
        public const int RejectNonstandard = 0x40;

        /// <summary>Public reject code for Dust transaction.</summary>
        public const int RejectDust = 0x41;

        /// <summary>Public reject code for Insufficient fee for transaction.</summary>
        public const int RejectInsufficientfee = 0x42;

        /// <summary>Public reject code for Transaction failed checkpoint.</summary>
        public const int RejectCheckpoint = 0x43;

        // Reject codes greater or equal to this can be returned by AcceptToMemPool
        // for transactions, to signal internal conditions. They cannot and should not
        // be sent over the P2P network.

        /// <summary>Internal rejection code for general rejection of transaction.</summary>
        public const int RejectInternal = 0x100;

        /// <summary>
        /// Internal rejection code for too high fee.
        /// Can not be triggered by P2P transactions.
        /// </summary>
        public const int RejectHighfee = 0x100;

        /// <summary>
        /// Internal rejection code for Transaction is already known.
        /// Either in mempool or blockchain.
        /// </summary>
        public const int RejectAlreadyKnown = 0x101;

        /// <summary>Internal rejection code for transaction conflicts with a transaction already known.</summary>
        public const int RejectConflict = 0x102;

        /// <summary>'coinbase' error returns a <see cref="RejectInvalid"/> reject code.</summary>
        public static MempoolError Coinbase = new MempoolError(RejectInvalid, "coinbase");

        /// <summary>'coinstake' error returns a <see cref="RejectInvalid"/> reject code.</summary>
        public static MempoolError Coinstake = new MempoolError(RejectInvalid, "coinstake");

        /// <summary>'non-final' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError NonFinal = new MempoolError(RejectNonstandard, "non-final");

        /// <summary>'txn-already-in-mempool' error returns a <see cref="RejectAlreadyKnown"/> reject code.</summary>
        public static MempoolError InPool = new MempoolError(RejectAlreadyKnown, "txn-already-in-mempool");

        /// <summary>'txn-mempool-conflict' error returns a <see cref="RejectConflict"/> reject code.</summary>
        public static MempoolError Conflict = new MempoolError(RejectConflict, "txn-mempool-conflict");

        /// <summary>'bad-txns-nonstandard-inputs' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError NonstandardInputs = new MempoolError(RejectNonstandard, "bad-txns-nonstandard-inputs");

        /// <summary>'bad-witness-nonstandard' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError NonstandardWitness = new MempoolError(RejectNonstandard, "bad-witness-nonstandard");

        /// <summary>'bad-txns-too-many-sigops' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError TooManySigops = new MempoolError(RejectNonstandard, "bad-txns-too-many-sigops");

        /// <summary>'mempool-full' error returns a <see cref="RejectInsufficientfee"/> reject code.</summary>
        public static MempoolError Full = new MempoolError(RejectInsufficientfee, "mempool-full");

        /// <summary>'insufficient-fee' error returns a <see cref="RejectInsufficientfee"/> reject code.</summary>
        public static MempoolError InsufficientFee = new MempoolError(RejectInsufficientfee, "insufficient-fee");

        /// <summary>'txn-already-known' error returns a <see cref="RejectAlreadyKnown"/> reject code.</summary>
        public static MempoolError AlreadyKnown = new MempoolError(RejectAlreadyKnown, "txn-already-known");

        /// <summary>'bad-txns-inputs-spent' error returns a <see cref="RejectDuplicate"/> reject code.</summary>
        public static MempoolError BadInputsSpent = new MempoolError(RejectDuplicate, "bad-txns-inputs-spent");

        /// <summary>'bad-txns-inputs-missing' error returns a <see cref="RejectInvalid"/> reject code.</summary>
        public static MempoolError MissingInputs = new MempoolError(RejectInvalid, "bad-txns-inputs-missing");

        /// <summary>'non-BIP68-final' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError NonBIP68Final = new MempoolError(RejectNonstandard, "non-BIP68-final");

        /// <summary>'mempool-min-fee-not-met' error returns a <see cref="RejectInsufficientfee"/> reject code.</summary>
        public static MempoolError MinFeeNotMet = new MempoolError(RejectInsufficientfee, "mempool-min-fee-not-met");

        /// <summary>'insufficient-priority' error returns a <see cref="RejectInsufficientfee"/> reject code.</summary>
        public static MempoolError InsufficientPriority = new MempoolError(RejectInsufficientfee, "insufficient-priority");

        /// <summary>'absurdly-high-fee' error returns a <see cref="RejectHighfee"/> reject code.</summary>
        public static MempoolError AbsurdlyHighFee = new MempoolError(RejectHighfee, $"absurdly-high-fee");

        /// <summary>'too-long-mempool-chain' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError TooLongMempoolChain = new MempoolError(RejectNonstandard, "too-long-mempool-chain");

        /// <summary>'bad-txns-spends-conflicting-tx' error returns a <see cref="RejectInvalid"/> reject code.</summary>
        public static MempoolError BadTxnsSpendsConflictingTx = new MempoolError(RejectInvalid, "bad-txns-spends-conflicting-tx");

        /// <summary>'too-many-potential-replacements' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError TooManyPotentialReplacements = new MempoolError(RejectNonstandard, "too-many-potential-replacements");

        /// <summary>'replacement-adds-unconfirmed' error returns a <see cref="RejectNonstandard"/> reject code.</summary>
        public static MempoolError ReplacementAddsUnconfirmed = new MempoolError(RejectNonstandard, "replacement-adds-unconfirmed");

        /// <summary>'insufficient-fee' error returns a <see cref="RejectInsufficientfee"/> reject code.</summary>
        public static MempoolError Insufficientfee = new MempoolError(RejectInsufficientfee, "insufficient-fee");

        /// <summary>'mandatory-script-verify-flag-failed' error returns a <see cref="RejectInvalid"/> reject code.</summary>
        public static MempoolError MandatoryScriptVerifyFlagFailed = new MempoolError(RejectInvalid, "mandatory-script-verify-flag-failed");

        /// <summary>'version' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError Version = new MempoolError(RejectNonstandard, "version");

        /// <summary>'tx-size' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError TxSize = new MempoolError(RejectNonstandard, "tx-size");

        /// <summary>'scriptsig-size' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError ScriptsigSize = new MempoolError(RejectNonstandard, "scriptsig-size");

        /// <summary>'scriptsig-not-pushonly' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError ScriptsigNotPushonly = new MempoolError(RejectNonstandard, "scriptsig-not-pushonly");

        /// <summary>'scriptpubkey' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError Scriptpubkey = new MempoolError(RejectNonstandard, "scriptpubkey");

        /// <summary>'dust' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError Dust = new MempoolError(RejectNonstandard, "dust");

        /// <summary>'multi-op-return' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError MultiOpReturn = new MempoolError(RejectNonstandard, "multi-op-return");

        /// <summary>'time-too-new' error returns a <see cref="RejectNonStandard"/> reject code.</summary>
        public static MempoolError TimeTooNew = new MempoolError(RejectNonstandard, "time-too-new");
    }
}
