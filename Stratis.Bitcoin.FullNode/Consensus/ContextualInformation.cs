using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class ContextBlockInformation
	{
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
		public Target NextWorkRequired
		{
			get;
			set;
		}
	}

	public class ContextInformation
	{
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
	}
}
