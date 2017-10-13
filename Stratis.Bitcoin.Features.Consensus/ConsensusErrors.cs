using System;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusErrorException : Exception
    {
        public ConsensusErrorException(ConsensusError error) : base(error.Message)
        {
            this.ConsensusError = error;
        }

        public ConsensusError ConsensusError
        {
            get;
            private set;
        }
    }
    public class ConsensusError
    {
        private readonly string _Code;
        public string Code
        {
            get
            {
                return this._Code;
            }
        }

        private readonly string _Message;
        public string Message
        {
            get
            {
                return this._Message;
            }
        }

        public void Throw()
        {
            throw new ConsensusErrorException(this);
        }

        public ConsensusError(string code, string message)
        {
            Guard.NotEmpty(code, nameof(code));
            Guard.NotEmpty(message, nameof(message));
            
            this._Code = code;
            this._Message = message;
        }

        public override bool Equals(object obj)
        {
            ConsensusError item = obj as ConsensusError;
            if(item == null)
                return false;
            return this.Code.Equals(item.Code);
        }
        public static bool operator ==(ConsensusError a, ConsensusError b)
        {
            if(System.Object.ReferenceEquals(a, b))
                return true;
            if(((object)a == null) || ((object)b == null))
                return false;
            return a.Code == b.Code;
        }

        public static bool operator !=(ConsensusError a, ConsensusError b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return this.Code.GetHashCode();
        }

        public override string ToString()
        {
            return this._Code + ": " + this._Message;
        }
    }
    public static class ConsensusErrors
    {
        public readonly static ConsensusError InvalidPrevTip = new ConsensusError("invalid-prev-tip", "invalid previous tip");
        public readonly static ConsensusError HighHash = new ConsensusError("high-hash", "proof of work failed");
        public readonly static ConsensusError BadCoinbaseHeight = new ConsensusError("bad-cb-height", "block height mismatch in coinbase");
        public readonly static ConsensusError BadTransactionNonFinal = new ConsensusError("bad-txns-nonfinal", "non-final transaction");
        public readonly static ConsensusError BadWitnessNonceSize = new ConsensusError("bad-witness-nonce-size", "invalid witness nonce size");
        public readonly static ConsensusError BadWitnessMerkleMatch = new ConsensusError("bad-witness-merkle-match", "witness merkle commitment mismatch");
        public readonly static ConsensusError UnexpectedWitness = new ConsensusError("unexpected-witness", "unexpected witness data found");
        public readonly static ConsensusError BadBlockWeight = new ConsensusError("bad-blk-weight", "weight limit failed");
        public readonly static ConsensusError BadDiffBits = new ConsensusError("bad-diffbits", "incorrect proof of work");
        public readonly static ConsensusError TimeTooOld = new ConsensusError("time-too-old", "block's timestamp is too early");
        public readonly static ConsensusError TimeTooNew = new ConsensusError("time-too-new", "block timestamp too far in the future");
        public readonly static ConsensusError BadVersion = new ConsensusError("bad-version", "block version rejected");
        public readonly static ConsensusError BadMerkleRoot = new ConsensusError("bad-txnmrklroot", "hashMerkleRoot mismatch");        
        public readonly static ConsensusError BadBlockLength = new ConsensusError("bad-blk-length", "size limits failed");
        public readonly static ConsensusError BadCoinbaseMissing = new ConsensusError("bad-cb-missing", "first tx is not coinbase");
        public readonly static ConsensusError BadCoinbaseSize = new ConsensusError("bad-cb-length", "invalid coinbase size");
        public readonly static ConsensusError BadMultipleCoinbase = new ConsensusError("bad-cb-multiple", "more than one coinbase");
        public readonly static ConsensusError BadMultipleCoinstake = new ConsensusError("bad-cs-multiple", "more than one coinstake");

        public readonly static ConsensusError BadBlockSigOps = new ConsensusError("bad-blk-sigops", "out-of-bounds SigOpCount");

        public readonly static ConsensusError BadTransactionDuplicate = new ConsensusError("bad-txns-duplicate", "duplicate transaction");
        public readonly static ConsensusError BadTransactionNoInput = new ConsensusError("bad-txns-vin-empty", "no input in the transaction");
        public readonly static ConsensusError BadTransactionNoOutput = new ConsensusError("bad-txns-vout-empty", "no output in the transaction");
        public readonly static ConsensusError BadTransactionOversize = new ConsensusError("bad-txns-oversize", "oversized transaction");
        public readonly static ConsensusError BadTransactionNegativeOutput = new ConsensusError("bad-txns-vout-negative", "the transaction contains a negative value output");
        public readonly static ConsensusError BadTransactionTooLargeOutput = new ConsensusError("bad-txns-vout-toolarge", "the transaction contains a too large value output");
        public readonly static ConsensusError BadTransactionTooLargeTotalOutput = new ConsensusError("bad-txns-txouttotal-toolarge", "the sum of outputs'value is too large for this transaction");
        public readonly static ConsensusError BadTransactionDuplicateInputs = new ConsensusError("bad-txns-inputs-duplicate", "duplicate inputs");
        public readonly static ConsensusError BadTransactionNullPrevout = new ConsensusError("bad-txns-prevout-null", "this transaction contains a null prevout");
        public readonly static ConsensusError BadTransactionBIP30 = new ConsensusError("bad-txns-BIP30", "tried to overwrite transaction");
        public readonly static ConsensusError BadTransactionMissingInput = new ConsensusError("bad-txns-inputs-missingorspent", "input missing/spent");

        public readonly static ConsensusError BadCoinbaseAmount = new ConsensusError("bad-cb-amount", "coinbase pays too much");
        public readonly static ConsensusError BadCoinstakeAmount = new ConsensusError("bad-cs-amount", "coinstake pays too much");

        public readonly static ConsensusError BadTransactionPrematureCoinbaseSpending = new ConsensusError("bad-txns-premature-spend-of-coinbase", "tried to spend coinbase before maturity");
        public readonly static ConsensusError BadTransactionPrematureCoinstakeSpending = new ConsensusError("bad-txns-premature-spend-of-coinstake", "tried to spend coinstake before maturity");

        public readonly static ConsensusError BadTransactionInputValueOutOfRange = new ConsensusError("bad-txns-inputvalues-outofrange", "input value out of range");
        public readonly static ConsensusError BadTransactionInBelowOut = new ConsensusError("bad-txns-in-belowout", "input value below output value");
        public readonly static ConsensusError BadTransactionNegativeFee = new ConsensusError("bad-txns-fee-negative", "negative fee");
        public readonly static ConsensusError BadTransactionFeeOutOfRange = new ConsensusError("bad-txns-fee-outofrange", "fee out of range");

        public readonly static ConsensusError BadTransactionScriptError = new ConsensusError("bad-txns-script-failed", "a script failed");

        public readonly static ConsensusError NonCoinstake = new ConsensusError("non-coinstake", "non-coinstake");
        public readonly static ConsensusError ReadTxPrevFailed = new ConsensusError("read-txPrev-failed", "read txPrev failed");
        public readonly static ConsensusError InvalidStakeDepth = new ConsensusError("invalid-stake-depth", "tried to stake at depth");
        public readonly static ConsensusError StakeTimeViolation = new ConsensusError("stake-time-violation", "stake time violation");
        public readonly static ConsensusError BadStakeBlock = new ConsensusError("bad-stake-block", "bad stake block");
        public readonly static ConsensusError PrevStakeNull = new ConsensusError("prev-stake-null", "previous stake is not found");
        public readonly static ConsensusError StakeHashInvalidTarget = new ConsensusError("proof-of-stake-hash-invalid-target", "proof-of-stake hash did not meet target protocol");

        public readonly static ConsensusError ModifierNotFound = new ConsensusError("modifier-not-found", "unable to get last modifier");
        public readonly static ConsensusError FailedSelectBlock = new ConsensusError("failed-select-block", "unable to select block at round");

        public readonly static ConsensusError SetStakeEntropyBitFailed = new ConsensusError("set-stake-entropy-bit-failed", "failed to set stake entropy bit");
        public readonly static ConsensusError CoinstakeVerifySignatureFailed = new ConsensusError("verify-signature-failed-on-coinstake", "verify signature failed on coinstake");
        public readonly static ConsensusError BlockTimestampTooFar = new ConsensusError("block-timestamp-to-far", "block timestamp too far in the future");
        public readonly static ConsensusError BlockTimestampTooEarly = new ConsensusError("block-timestamp-to-early", "block timestamp too early");
        public readonly static ConsensusError BadBlockSignature = new ConsensusError("bad-block-signature", "bad block signature");
        public readonly static ConsensusError BlockTimeBeforeTrx = new ConsensusError("block-time-before-trx", "block timestamp earlier than transaction timestamp");
        public readonly static ConsensusError ProofOfWorkTooHeigh = new ConsensusError("proof-of-work-too-heigh", "proof of work too heigh");

        public readonly static ConsensusError CheckpointViolation = new ConsensusError("checkpoint-violation", "block header hash does not match the checkpointed value");
    }
}
