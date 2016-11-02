using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class NullCoinView : CoinView
	{

		private static readonly NullCoinView _Instance;
		public static NullCoinView Instance
		{
			get
			{
				return _Instance;
			}
		}
		public NullCoinView()
		{

		}
		public override ChainedBlock Tip
		{
			get
			{
				return null;
			}
		}

		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			return new UnspentOutputs[txIds.Length];
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			
		}
	}
}
