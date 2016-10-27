using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
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
	public class CoinPairComparer : IComparer<Tuple<uint256, Coins>>
	{

		private static readonly CoinPairComparer _Instance = new CoinPairComparer();
		public static CoinPairComparer Instance
		{
			get
			{
				return _Instance;
			}
		}
		private readonly UInt256Comparer Comparer = new UInt256Comparer();
		public int Compare(Tuple<uint256, Coins> x, Tuple<uint256, Coins> y)
		{
			return Comparer.Compare(x.Item1, y.Item1);
		}
	}
}
