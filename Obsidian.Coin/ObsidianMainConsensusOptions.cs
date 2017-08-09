using NBitcoin;

namespace Obsidian.Coin
{
    public class ObsidianMainConsensusOptions
    {
	    // The maximum allowed size for a serialized block, in bytes (only for buffer size limits) 
	    public int MAX_BLOCK_SERIALIZED_SIZE = 4000000;

	    // The maximum allowed weight for a block, see BIP 141 (network rule) 
	    public int MAX_BLOCK_WEIGHT = 4000000;

	    public int WITNESS_SCALE_FACTOR  = 4;
	    public int SERIALIZE_TRANSACTION_NO_WITNESS  = 0x40000000;

	    // Changing the default transaction version requires a two step process: first
	    // adapting relay policy by bumping MAX_STANDARD_VERSION, and then later date
	    // bumping the default CURRENT_VERSION at which point both CURRENT_VERSION and
	    // MAX_STANDARD_VERSION will be equal.
	    public int MAX_STANDARD_VERSION  = 2;
	    // The maximum weight for transactions we're willing to relay/mine 
	    public int MAX_STANDARD_TX_WEIGHT  = 400000;
	    public int MAX_BLOCK_BASE_SIZE  = 1000000;
	    /** The maximum allowed number of signature check operations in a block (network rule) */
	    public int MAX_BLOCK_SIGOPS_COST  = 80000;
	    public long MAX_MONEY  = long.MaxValue;
	    public long COINBASE_MATURITY  = 50;
	    public Money ProofOfWorkReward  = Money.Coins(4);

		// Proof-Of-Stake
	    public Money ProofOfStakeReward  = Money.COIN;
	    public Money PremineReward  = Money.Coins(98000000);
	    public long PremineHeight = 2;
	    public long StakeMinConfirmations  = 50;
	    public long StakeMinAge  = 60; // 8 hours
	    public long StakeModifierInterval  = 10 * 60; // time to elapse before new modifier is computed
	}
}
