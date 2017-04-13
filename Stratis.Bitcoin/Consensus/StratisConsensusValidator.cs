using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class MemoryStakeChain : StakeChain
	{
		private readonly Network network;
		private Dictionary<uint256, BlockStake> items = new Dictionary<uint256, BlockStake>();

		public MemoryStakeChain(Network network)
		{
			this.network = network;
		}

		public override BlockStake Get(uint256 blockid)
		{
			return this.items.TryGet(blockid);
		}

		public sealed override void Set(uint256 blockid, BlockStake blockStake)
		{
			// throw if item already exists
			this.items.Add(blockid, blockStake);
		}
	}

	public class StratisConsensusValidator : ConsensusValidator
	{
		public StratisConsensusValidator(NBitcoin.Consensus consensusParams, ConsensusOptions consensusOptions) 
			: base(consensusParams, consensusOptions)
		{
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

		public override void ExecuteBlock(ContextInformation context, TaskScheduler taskScheduler, StakeChain stakeChain)
		{
			base.ExecuteBlock(context, taskScheduler, stakeChain);

			var blockstake = context.BlockStake;
			
			// TODO: calculate the stake modifiers

			stakeChain.Set(context.BlockResult.ChainedBlock.HashBlock, blockstake);
		}

		public override void CheckBlockHeader(ContextInformation context, StakeChain stakeChain)
		{
			var blockstake = new BlockStake(context.BlockResult.Block);
			context.BlockStake = blockstake;
			var header = context.BlockResult.Block.Header;

			if (blockstake.IsProofOfWork())
			{
				if (!header.CheckProofOfWork())
					ConsensusErrors.HighHash.Throw();

				context.NextWorkRequired = context.BlockResult.ChainedBlock.GetWorkRequired(context.Consensus);
			}
			else
			{
				context.NextWorkRequired = stakeChain.GetWorkRequired(context.BlockResult.ChainedBlock, blockstake, context.Consensus);
				if (header.Bits != context.NextWorkRequired)
					ConsensusErrors.HighHash.Throw();
			}

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
