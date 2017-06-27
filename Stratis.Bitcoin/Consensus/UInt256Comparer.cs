using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class UInt256Comparer : IComparer<uint256>
	{
		public int Compare(uint256 x, uint256 y)
		{
			if(x < y)
				return -1;
			if(x > y)
				return 1;
			return 0;
		}
	}
	public class UnspentOutputsComparer : IComparer<UnspentOutputs>
	{

		private static readonly UnspentOutputsComparer _Instance = new UnspentOutputsComparer();
		public static UnspentOutputsComparer Instance
		{
			get
			{
				return _Instance;
			}
		}
		private readonly UInt256Comparer Comparer = new UInt256Comparer();
		public int Compare(UnspentOutputs x, UnspentOutputs y)
		{
			return this.Comparer.Compare(x.TransactionId, y.TransactionId);
		}
	}
}
