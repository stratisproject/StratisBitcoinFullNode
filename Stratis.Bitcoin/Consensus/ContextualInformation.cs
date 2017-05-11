using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class ContextBlockInformation
	{
		public ContextBlockInformation()
		{

		}
		public ContextBlockInformation(ChainedBlock bestBlock, NBitcoin.Consensus consensus)
		{
			Guard.NotNull(bestBlock, nameof(bestBlock));
			
			Header = bestBlock.Header;
			Height = bestBlock.Height;
			MedianTimePast = bestBlock.GetMedianTimePast();
		}

		public BlockHeader Header
		{
			get;
			set;
		}
		public int Height
		{
			get;
			set;
		}
		public DateTimeOffset MedianTimePast
		{
			get;
			set;
		}		
	}

	public class ContextStakeInformation
	{
		public BlockStake BlockStake
		{
			get;
			set;
		}

		public Money TotalCoinStakeValueIn
		{
			get;
			set;
		}

		public uint256 HashProofOfStake
		{
			get;
			set;
		}

		public uint256 TargetProofOfStake

		{
			get;
			set;
		}
	}

	public class ContextInformation
	{
		public ContextInformation()
		{
			
		}

		public ContextInformation(BlockResult blockResult, NBitcoin.Consensus consensus)
		{
			Guard.NotNull(blockResult, nameof(blockResult));
			Guard.NotNull(consensus, nameof(consensus));

			this.BlockResult = blockResult;
			this.Consensus = consensus;
		}

		public void SetBestBlock()
		{
			BestBlock = new ContextBlockInformation(this.BlockResult.ChainedBlock.Previous, this.Consensus);
			Time = DateTimeOffset.UtcNow;
		}

		public void SetStake()
		{
			this.Stake = new ContextStakeInformation()
			{
				BlockStake = new BlockStake(this.BlockResult.Block)
			};
		}

		public NBitcoin.Consensus Consensus
		{
			get;
			set;
		}
		public DateTimeOffset Time
		{
			get;
			set;
		}

		public ContextBlockInformation BestBlock
		{
			get;
			set;
		}

		public ContextStakeInformation Stake
		{
			get;
			set;
		}

		public Target NextWorkRequired
		{
			get;
			set;
		}

		public BlockResult BlockResult
		{
			get;
			set;
		}

		public ConsensusFlags Flags
		{
			get;
			set;
		}

		public UnspentOutputSet Set
		{
			get;
			set;
		}

	}
}
