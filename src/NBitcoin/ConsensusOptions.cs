namespace NBitcoin
{
    /// <summary>
    /// An extension to <see cref="Consensus"/> to enable additional options to the consensus data.
    /// </summary>
    public class ConsensusOptions
    {
        /// <summary>The maximum allowed size for a serialized block, in bytes (only for buffer size limits).</summary>
        public int MaxBlockSerializedSize { get; set; }

        /// <summary>The maximum allowed weight for a block, see BIP 141 (network rule)</summary>
        public int MaxBlockWeight { get; set; }

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

        public int MaxBlockBaseSize { get; set; }

        /// <summary>The maximum allowed number of signature check operations in a block (network rule).</summary>
        public int MaxBlockSigopsCost { get; set; }

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public ConsensusOptions()
        {
            this.MaxBlockSerializedSize = 4000000;
            this.MaxBlockWeight = 4000000;
            this.WitnessScaleFactor = 4;
            this.SerializeTransactionNoWitness = 0x40000000;
            this.MaxStandardVersion = 2;
            this.MaxStandardTxWeight = 400000;
            this.MaxBlockBaseSize = 1000000;
            this.MaxBlockSigopsCost = 80000;
        }
    }
}
