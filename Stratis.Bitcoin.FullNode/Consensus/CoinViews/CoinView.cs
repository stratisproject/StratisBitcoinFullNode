using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{	
	public abstract class CoinView
	{
		public abstract void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs);
		public abstract ChainedBlock Tip
		{
			get;
		}
		public abstract UnspentOutputs[] FetchCoins(uint256[] txIds);		
	}
}
