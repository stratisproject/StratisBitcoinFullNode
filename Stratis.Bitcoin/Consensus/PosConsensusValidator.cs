using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class PosConsensusValidator : PowConsensusValidator
	{
		private readonly StakeChain stakeChain;

		public PosConsensusValidator(Network network, ConsensusOptions consensusOptions, StakeChain stakeChain) 
			: base(network, consensusOptions)
		{
			this.stakeChain = stakeChain;
		}

		public override void CheckBlockReward(ContextInformation context, Money nFees, ChainedBlock chainedBlock, Block block)
		{
			if (BlockStake.IsProofOfStake(block))
			{
				// proof of stake invalidates previous inputs 
				// and spends the inputs to new outputs with the 
				// additional  stake reward, this will calculate the  
				// reward does not exceed the consensu rules  

				var stakeReward = block.Transactions[1].TotalOut - context.TotalCoinStakeValueIn;
				var calcStakeReward = GetProofOfStakeReward(chainedBlock, nFees);

				if (stakeReward > calcStakeReward)
					ConsensusErrors.BadCoinstakeAmount.Throw();
			}
			else
			{
				var blockReward = GetProofOfWorkReward(chainedBlock, nFees);
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

		public Money GetProofOfWorkReward(ChainedBlock chainedBlock, long nFees)
		{
			if (this.IsPremine(chainedBlock))
				return this.ConsensusOptions.PremineReward;

			return this.ConsensusOptions.ProofOfWorkReward + nFees;
		}

		// miner's coin stake reward
		public Money GetProofOfStakeReward(ChainedBlock chainedBlock, long nFees)
		{
			if (this.IsPremine(chainedBlock))
				return this.ConsensusOptions.PremineReward;

			return this.ConsensusOptions.ProofOfStakeReward + nFees;
		}

		private bool IsPremine(ChainedBlock chainedBlock)
		{
			return this.ConsensusOptions.PremineHeight > 0 &&
			       this.ConsensusOptions.PremineReward > 0 &&
			       chainedBlock.Height == this.ConsensusOptions.PremineHeight;
		}
	}
}
