using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public static class Extensions
	{
		public static ChainedBlock FindFork(this ChainedBlock newTip, ChainedBlock tip)
		{
			var highest = newTip.Height > tip.Height ? newTip : tip;
			var lowest = highest == newTip ? tip : newTip;
			while(lowest.Height != highest.Height)
			{
				highest = highest.Previous;
			}
			while(lowest.HashBlock != highest.HashBlock)
			{
				lowest = lowest.Previous;
				highest = highest.Previous;
			}
			return highest;
		}
	}
}
