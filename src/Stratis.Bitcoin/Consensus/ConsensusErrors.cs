namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// A class that holds consensus errors.
    /// </summary>
    public static class ConsensusErrors
    {
        public static readonly ConsensusError InvalidPrevTip = new ConsensusError("invalid-prev-tip", "invalid previous tip");
        public static readonly ConsensusError HighHash = new ConsensusError("high-hash", "proof of work failed");
        public static readonly ConsensusError BadCoinbaseHeight = new ConsensusError("bad-cb-height", "block height mismatch in coinbase");
        public static readonly ConsensusError BadTransactionNonFinal = new ConsensusError("bad-txns-nonfinal", "non-final transaction");
        public static readonly ConsensusError BadWitnessNonceSize = new ConsensusError("bad-witness-nonce-size", "invalid witness nonce size");
        public static readonly ConsensusError BadWitnessMerkleMatch = new ConsensusError("bad-witness-merkle-match", "witness merkle commitment mismatch");
        public static readonly ConsensusError UnexpectedWitness = new ConsensusError("unexpected-witness", "unexpected witness data found");
        public static readonly ConsensusError BadBlockWeight = new ConsensusError("bad-blk-weight", "weight limit failed");
        public static readonly ConsensusError BadDiffBits = new ConsensusError("bad-diffbits", "incorrect proof of work");
        public static readonly ConsensusError TimeTooOld = new ConsensusError("time-too-old", "block's timestamp is too early");
        public static readonly ConsensusError TimeTooNew = new ConsensusError("time-too-new", "timestamp too far in the future");
        public static readonly ConsensusError BadVersion = new ConsensusError("bad-version", "block version rejected");
        public static readonly ConsensusError BadMerkleRoot = new ConsensusError("bad-txnmrklroot", "hashMerkleRoot mismatch");
        public static readonly ConsensusError BadBlockLength = new ConsensusError("bad-blk-length", "size limits failed");
        public static readonly ConsensusError BadCoinbaseMissing = new ConsensusError("bad-cb-missing", "first tx is not coinbase");
        public static readonly ConsensusError BadCoinbaseSize = new ConsensusError("bad-cb-length", "invalid coinbase size");
        public static readonly ConsensusError BadMultipleCoinbase = new ConsensusError("bad-cb-multiple", "more than one coinbase");
        public static readonly ConsensusError BadMultipleCoinstake = new ConsensusError("bad-cs-multiple", "more than one coinstake");

        public static readonly ConsensusError BadBlockSigOps = new ConsensusError("bad-blk-sigops", "out-of-bounds SigOpCount");

        public static readonly ConsensusError BadTransactionDuplicate = new ConsensusError("bad-txns-duplicate", "duplicate transaction");
        public static readonly ConsensusError BadTransactionNoInput = new ConsensusError("bad-txns-vin-empty", "no input in the transaction");
        public static readonly ConsensusError BadTransactionNoOutput = new ConsensusError("bad-txns-vout-empty", "no output in the transaction");
        public static readonly ConsensusError BadTransactionOversize = new ConsensusError("bad-txns-oversize", "oversized transaction");
        public static readonly ConsensusError BadTransactionEmptyOutput = new ConsensusError("user-txout-empty", "user transaction output is empty");
        public static readonly ConsensusError BadTransactionNegativeOutput = new ConsensusError("bad-txns-vout-negative", "the transaction contains a negative value output");
        public static readonly ConsensusError BadTransactionTooLargeOutput = new ConsensusError("bad-txns-vout-toolarge", "the transaction contains a too large value output");
        public static readonly ConsensusError BadTransactionTooLargeTotalOutput = new ConsensusError("bad-txns-txouttotal-toolarge", "the sum of outputs'value is too large for this transaction");
        public static readonly ConsensusError BadTransactionDuplicateInputs = new ConsensusError("bad-txns-inputs-duplicate", "duplicate inputs");
        public static readonly ConsensusError BadTransactionNullPrevout = new ConsensusError("bad-txns-prevout-null", "this transaction contains a null prevout");
        public static readonly ConsensusError BadTransactionBIP30 = new ConsensusError("bad-txns-BIP30", "tried to overwrite transaction");
        public static readonly ConsensusError BadTransactionMissingInput = new ConsensusError("bad-txns-inputs-missingorspent", "input missing/spent");

        public static readonly ConsensusError BadCoinbaseAmount = new ConsensusError("bad-cb-amount", "coinbase pays too much");
        public static readonly ConsensusError BadCoinstakeAmount = new ConsensusError("bad-cs-amount", "coinstake pays too much");

        public static readonly ConsensusError BadTransactionPrematureCoinbaseSpending = new ConsensusError("bad-txns-premature-spend-of-coinbase", "tried to spend coinbase before maturity");
        public static readonly ConsensusError BadTransactionPrematureCoinstakeSpending = new ConsensusError("bad-txns-premature-spend-of-coinstake", "tried to spend coinstake before maturity");

        public static readonly ConsensusError BadTransactionInputValueOutOfRange = new ConsensusError("bad-txns-inputvalues-outofrange", "input value out of range");
        public static readonly ConsensusError BadTransactionInBelowOut = new ConsensusError("bad-txns-in-belowout", "input value below output value");
        public static readonly ConsensusError BadTransactionNegativeFee = new ConsensusError("bad-txns-fee-negative", "negative fee");
        public static readonly ConsensusError BadTransactionFeeOutOfRange = new ConsensusError("bad-txns-fee-outofrange", "fee out of range");

        public static readonly ConsensusError BadTransactionScriptError = new ConsensusError("bad-txns-script-failed", "a script failed");

        public static readonly ConsensusError NonCoinstake = new ConsensusError("non-coinstake", "non-coinstake");
        public static readonly ConsensusError ReadTxPrevFailed = new ConsensusError("read-txPrev-failed", "read txPrev failed");
        public static readonly ConsensusError ReadTxPrevFailedInsufficient = new ConsensusError("read-txPrev-failed-insufficient", "read txPrev failed insufficient information");
        public static readonly ConsensusError InvalidStakeDepth = new ConsensusError("invalid-stake-depth", "tried to stake at depth");
        public static readonly ConsensusError StakeTimeViolation = new ConsensusError("stake-time-violation", "stake time violation");
        public static readonly ConsensusError BadStakeBlock = new ConsensusError("bad-stake-block", "bad stake block");
        public static readonly ConsensusError PrevStakeNull = new ConsensusError("prev-stake-null", "previous stake is not found");
        public static readonly ConsensusError StakeHashInvalidTarget = new ConsensusError("proof-of-stake-hash-invalid-target", "proof-of-stake hash did not meet target protocol");
        public static readonly ConsensusError EmptyCoinstake = new ConsensusError("empty-coinstake", "empty-coinstake");

        public static readonly ConsensusError ModifierNotFound = new ConsensusError("modifier-not-found", "unable to get last modifier");
        public static readonly ConsensusError FailedSelectBlock = new ConsensusError("failed-select-block", "unable to select block at round");

        public static readonly ConsensusError SetStakeEntropyBitFailed = new ConsensusError("set-stake-entropy-bit-failed", "failed to set stake entropy bit");
        public static readonly ConsensusError CoinstakeVerifySignatureFailed = new ConsensusError("verify-signature-failed-on-coinstake", "verify signature failed on coinstake");
        public static readonly ConsensusError BlockTimestampTooFar = new ConsensusError("block-timestamp-to-far", "block timestamp too far in the future");
        public static readonly ConsensusError BlockTimestampTooEarly = new ConsensusError("block-timestamp-to-early", "block timestamp too early");
        public static readonly ConsensusError BadBlockSignature = new ConsensusError("bad-block-signature", "bad block signature");
        public static readonly ConsensusError BlockTimeBeforeTrx = new ConsensusError("block-time-before-trx", "block timestamp earlier than transaction timestamp");
        public static readonly ConsensusError ProofOfWorkTooHigh = new ConsensusError("proof-of-work-too-heigh", "proof of work too high");

        public static readonly ConsensusError CheckpointViolation = new ConsensusError("checkpoint-violation", "block header hash does not match the checkpointed value");

        // Proven header validation errors.
        public static readonly ConsensusError BadProvenHeaderMerkleProofSize = new ConsensusError("proven-header-merkle-proof-size", "proven header's merkle proof size must be less than 512 bytes");
        public static readonly ConsensusError BadProvenHeaderCoinstakeSize = new ConsensusError("proven-header-coinstake-size", "proven header's coinstake size must be less than 1,000,000 bytes");
        public static readonly ConsensusError BadProvenHeaderSignatureSize = new ConsensusError("proven-header-signature-size", "proven header's signature size must be less than 80 bytes");
        public static readonly ConsensusError BadTransactionCoinstakeSpending = new ConsensusError("bad-txns-spend-of-coinstake", "coinstake is already spent");
        public static readonly ConsensusError UtxoNotFoundInRewindData = new ConsensusError("utxo-not-found-in-rewind-data", "utxo not found in rewind data");
        public static readonly ConsensusError InvalidPreviousProvenHeader = new ConsensusError("proven-header-invalid-previous-header", "previous header in chain is expected to be of proven header type");
        public static readonly ConsensusError InvalidPreviousProvenHeaderStakeModifier = new ConsensusError("proven-header-invalid-previous-header-stack-modifier", "previous proven header's StackModifier is null");

        public static readonly ConsensusError BadColdstakeAmount = new ConsensusError("bad-coldstake-amount", "coldstake is negative");
        public static readonly ConsensusError BadColdstakeInputs = new ConsensusError("bad-coldstake-inputs", "coldstake inputs contain mismatching scriptpubkeys");
        public static readonly ConsensusError BadColdstakeOutputs = new ConsensusError("bad-coldstake-outputs", "coldstake outputs contain unexpected scriptpubkeys");
    }
}
