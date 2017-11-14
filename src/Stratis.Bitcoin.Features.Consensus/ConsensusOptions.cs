using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus
{
    // The default setting of values on the consensus options
    // should be removed in to the initialization of each
    // network this are network specific values
    public class PosConsensusOptions : PowConsensusOptions
    {
        public new Money ProofOfWorkReward { get; set; }

        public Money ProofOfStakeReward { get; set; }

        public Money PremineReward { get; set; }

        public long PremineHeight { get; set; }

        public long StakeMinConfirmations { get; set; }

        public long StakeMinAge { get; set; }

        /// <summary>Time to elapse before new modifier is computed.</summary>
        public long StakeModifierInterval { get; set; }

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public PosConsensusOptions()
        {
            this.MaxMoney = long.MaxValue;
            this.CoinbaseMaturity = 50;

            this.ProofOfWorkReward = Money.Coins(4);
            this.ProofOfStakeReward = Money.COIN;
            this.PremineReward = Money.Coins(98000000);
            this.PremineHeight = 2;
            this.StakeMinConfirmations = 50;
            this.StakeMinAge = 60;
            this.StakeModifierInterval = 10 * 60;
            this.MaxReorgLength = 500;
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
        public long MaxMoney { get; set; }
        public long CoinbaseMaturity { get; set; }
        public Money ProofOfWorkReward { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        public uint MaxReorgLength { get; set; }

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
            this.MaxMoney = 21000000 * Money.COIN;
            this.CoinbaseMaturity = 100;
            this.ProofOfWorkReward = Money.Coins(50);

            // No long reorg protection on PoW.
            this.MaxReorgLength = 0;
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