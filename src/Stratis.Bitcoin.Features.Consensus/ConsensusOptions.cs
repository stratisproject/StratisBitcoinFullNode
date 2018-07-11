using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    // The default setting of values on the consensus options
    // should be removed in to the initialization of each
    // network this are network specific values
    public class PosConsensusOptions : PowConsensusOptions
    {
        /// <summary>Coinstake minimal confirmations softfork activation height for the mainnet.</summary>
        internal const int CoinstakeMinConfirmationActivationHeightMainnet = 1005000;

        /// <summary>Coinstake minimal confirmations softfork activation height for the testnet.</summary>
        internal const int CoinstakeMinConfirmationActivationHeightTestnet = 436000;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public PosConsensusOptions()
        {
        }

        /// <summary>
        /// Gets the minimum confirmations amount required for a coin to be good enough to participate in staking.
        /// </summary>
        /// <param name="height">Block height.</param>
        /// <param name="network">The network.</param>
        public virtual int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.IsTest())
                return height < CoinstakeMinConfirmationActivationHeightTestnet ? 10 : 20;

            return height < CoinstakeMinConfirmationActivationHeightMainnet ? 50 : 500;
        }
    }

    /// <summary>
    /// A set of options with default values of the Bitcoin network
    /// This can be easily overridable for alternative networks (i.e Stratis)
    /// Capital style param nameing is kept to mimic core
    /// </summary>
    public class PowConsensusOptions : NBitcoin.Consensus.ConsensusOptions
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
        public PowConsensusOptions()
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

    public static class ConsensusExtentions
    {
        public static T Option<T>(this NBitcoin.Consensus item)
            where T : NBitcoin.Consensus.ConsensusOptions
        {
            return item.Options as T;
        }
    }
}
