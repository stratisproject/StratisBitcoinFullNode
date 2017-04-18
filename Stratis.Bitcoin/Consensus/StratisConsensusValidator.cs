using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class StratisConsensusValidator : ConsensusValidator
	{
		private readonly StakeChain stakeChain;

		public StratisConsensusValidator(Network network, ConsensusOptions consensusOptions, StakeChain stakeChain) 
			: base(network, consensusOptions)
		{
			this.stakeChain = stakeChain;
		}

		public override void CheckBlockReward(Money nFees, ChainedBlock chainedBlock, Block block)
		{
			if (BlockStake.IsProofOfStake(block))
			{
				var blockReward = GetProofOfStakeReward(chainedBlock, nFees);
				if (block.Transactions[0].TotalOut > blockReward)
					ConsensusErrors.BadCoinbaseAmount.Throw();
			}
			else
			{
				var blockReward = GetProofOfWorkReward(chainedBlock.Previous);
				if (block.Transactions[0].TotalOut > blockReward)
					ConsensusErrors.BadCoinbaseAmount.Throw();
			}
		}

		public override void ExecuteBlock(ContextInformation context, TaskScheduler taskScheduler)
		{
			base.ExecuteBlock(context, taskScheduler);

			var blockstake = context.BlockStake;
			
			// TODO: calculate the stake modifiers

			stakeChain.Set(context.BlockResult.ChainedBlock.HashBlock, blockstake);
		}

		public override void CheckBlockHeader(ContextInformation context)
		{
			var blockstake = new BlockStake(context.BlockResult.Block);
			context.BlockStake = blockstake;

			if (blockstake.IsProofOfWork())
			{
				if (!context.BlockResult.Block.Header.CheckProofOfWork())
					ConsensusErrors.HighHash.Throw();
			}

			context.NextWorkRequired = stakeChain.GetWorkRequired(context.BlockResult.ChainedBlock, blockstake, context.Consensus);
		}

		public override void ContextualCheckBlockHeader(ContextInformation context)
		{
			Guard.NotNull(context.BestBlock, nameof(context.BestBlock));

			BlockHeader header = context.BlockResult.Block.Header;

			int nHeight = context.BestBlock.Height + 1;

			// Check proof of work
			if (header.Bits != context.NextWorkRequired)
				ConsensusErrors.BadDiffBits.Throw();

			// Check timestamp against prev
			if (header.BlockTime <= context.BestBlock.MedianTimePast)
				ConsensusErrors.TimeTooOld.Throw();

			// Check timestamp
			if (header.BlockTime > context.Time + TimeSpan.FromHours(2))
				ConsensusErrors.TimeTooNew.Throw();

			// Reject outdated version blocks when 95% (75% on testnet) of the network has upgraded:
			// check for version 2, 3 and 4 upgrades
			if ((header.Version < 2 && nHeight >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP34]) ||
			   (header.Version < 3 && nHeight >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP66]) ||
			   (header.Version < 4 && nHeight >= this.ConsensusParams.BuriedDeployments[BuriedDeployments.BIP65]))
				ConsensusErrors.BadVersion.Throw();
		}

		public static Money GetProofOfWorkReward(ChainedBlock chainedBlock)
		{
			if (chainedBlock.Height == 1)
			{
				long PreMine = Money.Coins(98000000);
				return PreMine;
			}

			return Money.Coins(4);
		}

		// miner's coin stake reward
		public static Money GetProofOfStakeReward(ChainedBlock pindexPrev, long nFees)
		{
			return Money.Coins(1) + nFees;
		}
	}
}
