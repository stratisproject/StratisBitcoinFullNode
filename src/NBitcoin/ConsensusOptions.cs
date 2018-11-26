namespace NBitcoin
{
    /// <summary>
    /// An extension to <see cref="Consensus"/> to enable additional options to the consensus data.
    /// TODO: Make immutable.
    /// </summary>
    public class ConsensusOptions
    {
        /// <summary>
        /// Flag used to detect SegWit transactions.
        /// </summary>
        public const int SerializeTransactionNoWitness = 0x40000000;

        /// <summary>Maximum size for a block in bytes. </summary>
        public uint MaxBlockBaseSize { get; set; }

        /// <summary>The maximum allowed weight for a block, see BIP 141 (network rule)</summary>
        public uint MaxBlockWeight { get; set; }

        /// <summary>The maximum allowed size for a serialized block, in bytes (only for buffer size limits). </summary>
        public uint MaxBlockSerializedSize { get; set; }

        /// <summary>Scale of witness vs other transaction data. e.g. if set to 4, then witnesses have 1/4 the weight per byte of other transaction data. </summary>
        public int WitnessScaleFactor { get; set; }

        /// <summary>
        /// Changing the default transaction version requires a two step process:
        /// <list type="bullet">
        /// <item>Adapting relay policy by bumping <see cref="MaxStandardVersion"/>,</item>
        /// <item>and then later date bumping the default CURRENT_VERSION at which point both CURRENT_VERSION and
        /// <see cref="MaxStandardVersion"/> will be equal.</item>
        /// </list>
        /// </summary>
        public int MaxStandardVersion { get; set; }

        /// <summary>The maximum weight for transactions we're willing to relay/mine.</summary>
        public int MaxStandardTxWeight { get; set; }

        /// <summary>The maximum allowed number of signature check operations in a block (network rule).</summary>
        public int MaxBlockSigopsCost { get; set; }

        /// <summary>The maximum number of sigops we're willing to relay/mine in a single tx.</summary>
        public int MaxStandardTxSigopsCost { get; set; }

        /// <summary>
        /// Initializes the default values. Currently only used for initialising Bitcoin networks and testing.
        /// </summary>
        public ConsensusOptions()
        {
            // TODO: Remove this constructor. Should always set explicitly.
            this.MaxBlockSerializedSize = 4000000;
            this.MaxBlockWeight = 4000000;
            this.WitnessScaleFactor = 4;
            this.MaxStandardVersion = 2;
            this.MaxStandardTxWeight = 400000;
            this.MaxBlockBaseSize = 1000000;
            this.MaxBlockSigopsCost = 80000;
            this.MaxStandardTxSigopsCost = this.MaxBlockSigopsCost / 5;
        }

        /// <summary>
        /// Initializes all values. Used by networks that use block weight rules.
        /// </summary>
        public ConsensusOptions(
            uint maxBlockBaseSize,
            uint maxBlockWeight,
            uint maxBlockSerializedSize,
            int witnessScaleFactor,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost)
        {
            this.MaxBlockBaseSize = maxBlockBaseSize;
            this.MaxBlockWeight = maxBlockWeight;
            this.MaxBlockSerializedSize = maxBlockSerializedSize;
            this.WitnessScaleFactor = witnessScaleFactor;
            this.MaxStandardVersion = maxStandardVersion;
            this.MaxStandardTxWeight = maxStandardTxWeight;
            this.MaxBlockSigopsCost = maxBlockSigopsCost;
            this.MaxStandardTxSigopsCost = maxStandardTxSigopsCost;
        }

        /// <summary>
        /// Initializes values for networks that use block size rules.
        /// </summary>
        public ConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost)
        {
            this.MaxBlockBaseSize = maxBlockBaseSize;

            // Having witnessScale = 1 and setting the weight to be the same value as the base size
            // will result in all checks comparing size in bytes.
            this.MaxBlockWeight = maxBlockBaseSize;
            this.MaxBlockSerializedSize = maxBlockBaseSize;
            this.WitnessScaleFactor = 1;

            this.MaxStandardVersion = maxStandardVersion;
            this.MaxStandardTxWeight = maxStandardTxWeight;
            this.MaxBlockSigopsCost = maxBlockSigopsCost;
            this.MaxStandardTxSigopsCost = maxStandardTxSigopsCost;
        }
    }

    /// <summary>
    /// Extension to ConsensusOptions for PoS-related parameters.
    ///
    /// TODO: When moving rules to be part of consensus for network, move this class to the appropriate project too.
    /// Doesn't make much sense for it to be in NBitcoin. Also remove the CoinstakeMinConfirmation consts and set CointakeMinConfirmation in Network building.
    /// </summary>
    public class PosConsensusOptions : ConsensusOptions
    {
        /// <summary>Coinstake minimal confirmations softfork activation height for mainnet.</summary>
        public const int CoinstakeMinConfirmationActivationHeightMainnet = 1005000;

        /// <summary>Coinstake minimal confirmations softfork activation height for testnet.</summary>
        public const int CoinstakeMinConfirmationActivationHeightTestnet = 436000;

        /// <summary>A mask for coinstake transaction's timestamp and header's timestamp.</summary>
        /// <remarks>Used to decrease granularity of timestamp. Supposed to be 2^n-1.</remarks>
        public const uint StakeTimestampMask = 0x0000000F;

        /// <summary>
        /// Maximum coinstake serialized size in bytes.
        /// </summary>
        public const int MaxCoinstakeSerializedSize = 1_000_000;

        /// <summary>
        /// Maximum signature serialized size in bytes.
        /// </summary>
        public const int MaxBlockSignatureSerializedSize = 80;

        /// <summary>
        /// Maximum merkle proof serialized size in bytes.
        /// </summary>
        public const int MaxMerkleProofSerializedSize = 512;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public PosConsensusOptions()
        {
        }

        /// <summary>
        /// Initializes all values. Used by networks that use block weight rules.
        /// </summary>
        public PosConsensusOptions(
            uint maxBlockBaseSize,
            uint maxBlockWeight,
            uint maxBlockSerializedSize,
            int witnessScaleFactor,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost) : base(maxBlockBaseSize, maxBlockWeight, maxBlockSerializedSize, witnessScaleFactor, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
        }

        /// <summary>
        /// Initializes values for networks that use block size rules.
        /// </summary>
        public PosConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost
            ) : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
        }

        /// <summary>
        /// Gets the minimum confirmations amount required for a coin to be good enough to participate in staking.
        /// </summary>
        /// <param name="height">Block height.</param>
        /// <param name="network">The network.</param>
        public virtual int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.Name.ToLowerInvariant().Contains("test")) // TODO: When rules are moved to network, we can use the extension method IsTest() from Stratis.Bitcoin.
                return height < CoinstakeMinConfirmationActivationHeightTestnet ? 10 : 20;

            return height < CoinstakeMinConfirmationActivationHeightMainnet ? 50 : 500;
        }
    }
}
