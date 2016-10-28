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
		public abstract Coins AccessCoins(uint256 txId);
		public abstract void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins);
		public abstract ChainedBlock Tip
		{
			get;
		}

		public TxOut GetOutputFor(TxIn txIn)
		{
			var c = AccessCoins(txIn.PrevOut.Hash);
			if(c == null)
				return null;
			if(!c.IsAvailable(txIn.PrevOut.N))
				return null;
			return c.Outputs[(int)txIn.PrevOut.N];
		}

		public Money GetValueIn(Transaction tx)
		{
			return tx
			.Inputs
			.Select(i => GetOutputFor(i).Value)
			.Sum();
		}

		public virtual Coins[] FetchCoins(uint256[] txIds)
		{
			Coins[] coins = new Coins[txIds.Length];
			for(int i  = 0; i < coins.Length; i++)
			{
				coins[i] = AccessCoins(txIds[i]);
			}
			return coins;
		}

		public bool HaveInputs(Transaction tx)
		{
			foreach(var input in tx.Inputs)
			{
				var coin = AccessCoins(input.PrevOut.Hash);
				if(coin == null || !coin.IsAvailable(input.PrevOut.N))
					return false;
			}
			return true;
		}
	}
}
