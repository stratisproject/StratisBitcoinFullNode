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

        /// <summary>Maximal length of reorganization that the node is willing to accept.</summary>
        public uint MaxReorgLength { get; set; }

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public PosConsensusOptions()
        {
            this.MAX_MONEY = long.MaxValue;
            this.COINBASE_MATURITY = 50;

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
        // The maximum allowed size for a serialized block, in bytes (only for buffer size limits) 
        public int MAX_BLOCK_SERIALIZED_SIZE = 4000000;

        // The maximum allowed weight for a block, see BIP 141 (network rule) 
        public int MAX_BLOCK_WEIGHT { get; set; } = 4000000;

        public int WITNESS_SCALE_FACTOR { get; set; } = 4;
        public int SERIALIZE_TRANSACTION_NO_WITNESS { get; set; } = 0x40000000;

        // Changing the default transaction version requires a two step process: first
        // adapting relay policy by bumping MAX_STANDARD_VERSION, and then later date
        // bumping the default CURRENT_VERSION at which point both CURRENT_VERSION and
        // MAX_STANDARD_VERSION will be equal.
        public int MAX_STANDARD_VERSION { get; set; } = 2;
        // The maximum weight for transactions we're willing to relay/mine 
        public int MAX_STANDARD_TX_WEIGHT { get; set; } = 400000;
        public int MAX_BLOCK_BASE_SIZE { get; set; } = 1000000;
        /** The maximum allowed number of signature check operations in a block (network rule) */
        public int MAX_BLOCK_SIGOPS_COST { get; set; } = 80000;
        public long MAX_MONEY { get; set; } = 21000000 * Money.COIN;
        public long COINBASE_MATURITY { get; set; } = 100;
        public Money ProofOfWorkReward { get; set; } = Money.Coins(50);
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