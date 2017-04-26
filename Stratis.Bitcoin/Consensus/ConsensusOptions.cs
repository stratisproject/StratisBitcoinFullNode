using System;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
	public class StratisConsensusOptions : BitcoinConsensusOptions
	{
		public override long MAX_MONEY => long.MaxValue;
		public override long COINBASE_MATURITY => 50;

		public override Money ProofOfWorkReward => Money.Coins(4);

		public override Money ProofOfStakeReward => Money.COIN;

		public override Money PremineReward => Money.Coins(98000000);

		public override long PremineHeight => 2;

		public override long StakeMinConfirmations => 50;

		public override long StakeMinAge => 60; // 8 hours

		public override long StakeModifierInterval => 10 * 60; // time to elapse before new modifier is computed

	}

	/// <summary>
	/// A set of options with default values of the Bitcoin network
	/// This can be easily overridable for alternative networks (i.e Stratis)
	/// Capital style param nameing is kept to mimic core
	/// </summary>
	public class BitcoinConsensusOptions : ConsensusOptions
	{
		public override int MAX_BLOCK_WEIGHT => 4000000;

		public override int WITNESS_SCALE_FACTOR => 4;
		public override int SERIALIZE_TRANSACTION_NO_WITNESS => 0x40000000;

		// Changing the default transaction version requires a two step process: first
		// adapting relay policy by bumping MAX_STANDARD_VERSION, and then later date
		// bumping the default CURRENT_VERSION at which point both CURRENT_VERSION and
		// MAX_STANDARD_VERSION will be equal.
		public override int MAX_STANDARD_VERSION => 2;
		// The maximum weight for transactions we're willing to relay/mine 
		public override int MAX_STANDARD_TX_WEIGHT => 400000;
		public override int MAX_BLOCK_BASE_SIZE => 1000000;
		/** The maximum allowed number of signature check operations in a block (network rule) */
		public override int MAX_BLOCK_SIGOPS_COST => 80000;
		public override long MAX_MONEY => 21000000 * Money.COIN;
		public override long COINBASE_MATURITY => 100;
		public override Money ProofOfWorkReward => Money.Coins(50);

		public override Money ProofOfStakeReward
		{
			get { throw new NotImplementedException(); }
		}

		public override Money PremineReward
		{
			get { throw new NotImplementedException(); }
		}
	}

	public abstract class ConsensusOptions
	{
		public abstract int MAX_BLOCK_WEIGHT { get; }
		public abstract int WITNESS_SCALE_FACTOR { get; }
		public abstract int SERIALIZE_TRANSACTION_NO_WITNESS { get; }

		// Changing the default transaction version requires a two step process: first
		// adapting relay policy by bumping MAX_STANDARD_VERSION, and then later date
		// bumping the default CURRENT_VERSION at which point both CURRENT_VERSION and
		// MAX_STANDARD_VERSION will be equal.
		public abstract int MAX_STANDARD_VERSION { get; }
		// The maximum weight for transactions we're willing to relay/mine 
		public abstract int MAX_STANDARD_TX_WEIGHT { get; }
		public abstract int MAX_BLOCK_BASE_SIZE { get; }
		/** The maximum allowed number of signature check operations in a block (network rule) */
		public abstract int MAX_BLOCK_SIGOPS_COST { get; }
		public abstract long MAX_MONEY { get; }
		public abstract long COINBASE_MATURITY { get; }

		public abstract Money ProofOfWorkReward { get; }
		public abstract Money ProofOfStakeReward { get; }
		public virtual Money PremineReward => 0 ;
		public virtual long PremineHeight => 0;
		public virtual long StakeMinConfirmations => 0;

		public virtual long StakeMinAge => 0;
		public virtual long StakeModifierInterval => 0;

	}
}