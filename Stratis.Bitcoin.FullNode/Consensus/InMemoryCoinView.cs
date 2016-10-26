using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class InMemoryCoinView : CoinView
	{
		public InMemoryCoinView()
		{
			CanRemove = true;
		}
		public InMemoryCoinView(ChainedBlock tip)
		{
			CanRemove = true;
			_Tip = tip;
		}
		ChainedBlock _Tip;
		public override ChainedBlock Tip
		{
			get
			{
				return _Tip;
			}
		}

		public override Coins AccessCoins(uint256 txId)
		{
			Coins r;
			coins.TryGetValue(txId, out r);
			return r;
		}

		public bool CanRemove
		{
			get;
			set;
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			_Tip = newTip;
			var enumTxIds = txIds.GetEnumerator();
			var enumCoins = coins.GetEnumerator();
			while(enumTxIds.MoveNext())
			{
				enumCoins.MoveNext();
				SaveChange(enumTxIds.Current, enumCoins.Current);
			}
		}

		internal Dictionary<uint256, Coins> coins = new Dictionary<uint256, Coins>();
		public void SaveChange(uint256 txid, Coins coins)
		{
			if(coins.IsPruned && CanRemove)
			{
				this.coins.Remove(txid);
			}
			else
			{
				this.coins.AddOrReplace(txid, coins);
			}
		}

		internal void SaveChanges(Transaction tx, int height)
		{
			tx = tx.Clone();
			coins.AddOrReplace(tx.GetHash(), new Coins(tx, height));
			if(!tx.IsCoinBase)
			{
				foreach(var input in tx.Inputs)
				{
					var c = AccessCoins(input.PrevOut.Hash);
					c.Spend((int)input.PrevOut.N);
					if(c.IsPruned && CanRemove)
						coins.Remove(input.PrevOut.Hash);
				}
			}
		}
	}
}
