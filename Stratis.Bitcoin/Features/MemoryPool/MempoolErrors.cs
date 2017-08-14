using Stratis.Bitcoin.Features.Consensus;
using System;

namespace Stratis.Bitcoin.Features.MemoryPool
{
    public class MempoolErrorException : Exception
    {
        public MempoolErrorException(MempoolValidationState state) : base(state.ErrorMessage)
        {
            this.ValidationState = state;
        }

        public MempoolValidationState ValidationState
        {
            get;
            private set;
        }
    }

    public class MempoolError 
    {
        public MempoolError()
        {
        }

        public MempoolError(int rejectCode, string code)
        {
            this.Code = code;
            this.RejectCode = rejectCode;
        }

        public MempoolError(ConsensusError consensusError)
        {
            this.ConsensusError = consensusError;
        }

        public string Code { get; set; }
        public int RejectCode { get; set; }

        public ConsensusError ConsensusError { get; set; }
    }

    public static class MempoolErrors
    {
        //  "reject" message codes 
        public const int RejectMalformed = 0x01;
        public const int RejectInvalid = 0x10;
        public const int RejectObsolete = 0x11;
        public const int RejectDuplicate = 0x12;
        public const int RejectNonstandard = 0x40;
        public const int RejectDust = 0x41;
        public const int RejectInsufficientfee = 0x42;
        public const int RejectCheckpoint = 0x43;
        // Reject codes greater or equal to this can be returned by AcceptToMemPool
        // for transactions, to signal internal conditions. They cannot and should not
        // be sent over the P2P network.
        public const int RejectInternal = 0x100;
        // Too high fee. Can not be triggered by P2P transactions 
        public const int RejectHighfee = 0x100;
        // Transaction is already known (either in mempool or blockchain) 
        public const int RejectAlreadyKnown = 0x101;
        // Transaction conflicts with a transaction already known 
        public const int RejectConflict = 0x102;

        public static MempoolError Coinbase = new MempoolError(RejectInvalid, "coinbase");
        public static MempoolError NonFinal = new MempoolError(RejectNonstandard, "non-final");
        public static MempoolError InPool = new MempoolError(RejectAlreadyKnown, "txn-already-in-mempool");
        public static MempoolError Conflict = new MempoolError(RejectConflict, "txn-mempool-conflict");
        public static MempoolError NonstandardInputs = new MempoolError(RejectNonstandard, "bad-txns-nonstandard-inputs");
        public static MempoolError TooManySigops = new MempoolError(RejectNonstandard, "bad-txns-too-many-sigops");
        public static MempoolError Full = new MempoolError(RejectInsufficientfee, "mempool-full");
        public static MempoolError InsufficientFee = new MempoolError(RejectInsufficientfee, "insufficient-fee");
        public static MempoolError AlreadyKnown = new MempoolError(RejectAlreadyKnown, "txn-already-known");
        public static MempoolError BadInputsSpent = new MempoolError(RejectDuplicate, "bad-txns-inputs-spent");
        public static MempoolError NonBIP68Final = new MempoolError(RejectNonstandard, "non-BIP68-final");
        public static MempoolError MinFeeNotMet = new MempoolError(RejectInsufficientfee,"mempool-min-fee-not-met");
        public static MempoolError InsufficientPriority = new MempoolError(RejectInsufficientfee, "insufficient-priority");
        public static MempoolError AbsurdlyHighFee = new MempoolError(RejectHighfee, $"absurdly-high-fee");
        public static MempoolError TooLongMempoolChain = new MempoolError(RejectNonstandard, "too-long-mempool-chain");
        public static MempoolError BadTxnsSpendsConflictingTx = new MempoolError(RejectInvalid, "bad-txns-spends-conflicting-tx");
        public static MempoolError TooManyPotentialReplacements = new MempoolError(RejectNonstandard, "too-many-potential-replacements");
        public static MempoolError ReplacementAddsUnconfirmed = new MempoolError(RejectNonstandard, "replacement-adds-unconfirmed");
        public static MempoolError Insufficientfee = new MempoolError(RejectInsufficientfee, "insufficient-fee");
        public static MempoolError MandatoryScriptVerifyFlagFailed = new MempoolError(RejectInvalid, "mandatory-script-verify-flag-failed");
        public static MempoolError Version = new MempoolError(RejectNonstandard, "version");
        public static MempoolError TxSize = new MempoolError(RejectNonstandard, "tx-size");
        public static MempoolError ScriptsigSize = new MempoolError(RejectNonstandard, "scriptsig-size");
        public static MempoolError ScriptsigNotPushonly = new MempoolError(RejectNonstandard, "scriptsig-not-pushonly");
        public static MempoolError Scriptpubkey = new MempoolError(RejectNonstandard, "scriptpubkey");
        public static MempoolError Dust = new MempoolError(RejectNonstandard, "dust");
        public static MempoolError MultiOpReturn = new MempoolError(RejectNonstandard, "multi-op-return");
    }

}
