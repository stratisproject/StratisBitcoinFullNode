using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class InMemoryCoinView : CoinView
	{
		public InMemoryCoinView()
		{
			RemovePrunableCoins = true;
		}
		public InMemoryCoinView(ChainedBlock tip)
		{
			RemovePrunableCoins = true;
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
			return AccessCoins(txId, true);
		}

		private Coins AccessCoins(uint256 txId, bool copy)
		{			
			Coins r;
			coins.TryGetValue(txId, out r);
			if(r == null)
				return null;
			return copy ? r.Clone() : r;
		}

		public bool RemovePrunableCoins
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
		public void SaveChange(uint256 txId, Coins coins)
		{			
			if(coins == null || (coins.IsPruned && RemovePrunableCoins))
			{
				this.coins.Remove(txId);
			}
			else
			{
				AddOrMerge(txId, coins);
			}
		}

		private void AddOrMerge(uint256 txid, Coins c)
		{
			var old = coins.TryGet(txid);
			if(old == null)
			{
				coins.Add(txid, c);
				return;
			}
			old.MergeFrom(c);
		}

		internal void SaveChanges(Transaction tx, int height)
		{
			coins.AddOrReplace(tx.GetHash(), new Coins(tx, height));
			if(!tx.IsCoinBase)
			{
				foreach(var input in tx.Inputs)
				{
					var c = AccessCoins(input.PrevOut.Hash, false);
					c.Spend((int)input.PrevOut.N);
					if(RemovePrunableCoins && c.IsPruned)
					{
						coins.Remove(input.PrevOut.Hash);
					}
				}
			}
		}

		public void Clear()
		{
			coins.Clear();
		}
	}
}
