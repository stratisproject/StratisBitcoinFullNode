namespace NBitcoin
{
    /// <summary>
    /// An extension to <see cref="Consensus"/> to enable additional options to the consensus data.
    /// </summary>
    public class ConsensusOptions
    {
        /// <summary>Maximum size for a block in bytes. </summary>
        public int MaxBlockBaseSize { get; set; }

        /// <summary>The maximum allowed weight for a block, see BIP 141 (network rule)</summary>
        public int MaxBlockWeight { get; set; }

        /// <summary>The maximum allowed size for a serialized block, in bytes (only for buffer size limits). </summary>
        public int MaxBlockSerializedSize { get; set; }

        /// <summary>Scale of witness vs other transaction data. e.g. if set to 4, then witnesses have 1/4 the weight per byte of other transaction data. </summary>
        public int WitnessScaleFactor { get; set; }

        public int SerializeTransactionNoWitness { get; set; }

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

        /// <summary>
        /// Initializes the default values. Currently only used for testing.
        /// </summary>
        public ConsensusOptions()
        {
            // TODO: Remove this constructor. Should always set explicitly.
            this.MaxBlockSerializedSize = 4000000;
            this.MaxBlockWeight = 4000000;
            this.WitnessScaleFactor = 4;
            this.SerializeTransactionNoWitness = 0x40000000;
            this.MaxStandardVersion = 2;
            this.MaxStandardTxWeight = 400000;
            this.MaxBlockBaseSize = 1000000;
            this.MaxBlockSigopsCost = 80000;
        }


        /// <summary>
        /// Initializes all values. Used by networks that use block weight rules.
        /// </summary>
        public ConsensusOptions(
            int maxBlockBaseSize,
            int maxBlockWeight,
            int maxBlockSerializedSize,
            int witnessScaleFactor,
            int serializeTransactionNoWitness,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost)
        {
            this.MaxBlockBaseSize = maxBlockBaseSize;
            this.MaxBlockWeight = maxBlockWeight;
            this.MaxBlockSerializedSize = maxBlockSerializedSize;
            this.WitnessScaleFactor = witnessScaleFactor;
            this.SerializeTransactionNoWitness = serializeTransactionNoWitness;
            this.MaxStandardVersion = maxStandardVersion;
            this.MaxStandardTxWeight = maxStandardTxWeight;
            this.MaxBlockSigopsCost = maxBlockSigopsCost;
        }

        /// <summary>
        /// Initializes values for networks that use block size rules.
        /// </summary>
        public ConsensusOptions(
            int maxBlockBaseSize,
            int serializeTransactionNoWitness,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost)
        {
            this.MaxBlockBaseSize = maxBlockBaseSize;

            // Having witnessScale = 1 and setting the weight to be the same value as the base size
            // will result in all checks comparing size in bytes.
            this.MaxBlockWeight = maxBlockBaseSize;
            this.MaxBlockSerializedSize = maxBlockBaseSize;
            this.WitnessScaleFactor = 1;

            this.SerializeTransactionNoWitness = serializeTransactionNoWitness;
            this.MaxStandardVersion = maxStandardVersion;
            this.MaxStandardTxWeight = maxStandardTxWeight;
            this.MaxBlockSigopsCost = maxBlockSigopsCost;
        }
    }
}
