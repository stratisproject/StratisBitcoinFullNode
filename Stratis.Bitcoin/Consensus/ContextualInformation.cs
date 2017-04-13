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

	public class ContextInformation
	{
		public ContextInformation()
		{
			
		}

		public ContextInformation(BlockResult blockResult, NBitcoin.Consensus consensus, ConsensusOptions options)
		{
			Guard.NotNull(blockResult, nameof(blockResult));
			Guard.NotNull(consensus, nameof(consensus));
			Guard.NotNull(options, nameof(options));

			this.BlockResult = blockResult;
			this.Consensus = consensus;
			this.ConsensusOptions = options;

		}

		public void SetChain(StakeChain stakeChain)
		{
			BestBlock = new ContextBlockInformation(this.BlockResult.ChainedBlock.Previous, this.Consensus);
			Time = DateTimeOffset.UtcNow;
		}

		public NBitcoin.Consensus Consensus
		{
			get;
			set;
		}
		public ConsensusOptions ConsensusOptions
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

		public BlockStake BlockStake
		{
			get;
			set;
		}
	}
}
