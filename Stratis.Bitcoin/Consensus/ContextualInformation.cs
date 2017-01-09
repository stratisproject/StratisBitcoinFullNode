using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class ContextBlockInformation
	{
		public ContextBlockInformation()
		{

		}
		public ContextBlockInformation(ChainedBlock bestBlock, NBitcoin.Consensus consensus)
		{
			if(bestBlock == null)
				throw new ArgumentNullException("bestBlock");       
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

		public ContextInformation(ChainedBlock nextBlock, NBitcoin.Consensus consensus)
		{
			if(nextBlock == null)
				throw new ArgumentNullException("nextBlock");
			BestBlock = new ContextBlockInformation(nextBlock.Previous, consensus);
			Time = DateTimeOffset.UtcNow;
			NextWorkRequired = nextBlock.GetWorkRequired(consensus);
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
	}
}
