using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
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

		public bool SpendOnly
		{
			get;
			set;
		}
		ChainedBlock _Tip;
		public override ChainedBlock Tip
		{
			get
			{
				return _Tip;
			}
		}

		public UnspentOutputs AccessCoins(uint256 txId)
		{
			return AccessCoins(txId, true);
		}

		private UnspentOutputs AccessCoins(uint256 txId, bool copy)
		{			
			UnspentOutputs r;
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

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			_Tip = newTip;
			foreach(var output in unspentOutputs)
			{
				SaveChange(output.TransactionId, output);
			}
		}

		internal Dictionary<uint256, UnspentOutputs> coins = new Dictionary<uint256, UnspentOutputs>();
		public void SaveChange(uint256 txId, UnspentOutputs coins)
		{			
			if(coins == null || (coins.IsPrunable && RemovePrunableCoins))
			{
				this.coins.Remove(txId);
			}
			else
			{
				AddOrMerge(txId, coins);
			}
		}

		private void AddOrMerge(uint256 txId, UnspentOutputs c)
		{
			var old = coins.TryGet(txId);
			if(old == null)
			{
				if(!SpendOnly)
					coins.Add(txId, c);
				return;
			}
			old.MergeFrom(c);
		}

		internal void SaveChanges(Transaction tx, int height)
		{
			coins.AddOrReplace(tx.GetHash(), new UnspentOutputs((uint)height, tx));
			if(!tx.IsCoinBase)
			{
				foreach(var input in tx.Inputs)
				{
					var c = AccessCoins(input.PrevOut.Hash, false);
					c.Spend(input.PrevOut.N);
					if(RemovePrunableCoins && c.IsPrunable)
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

		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			var result = new UnspentOutputs[txIds.Length];
			for(int i = 0; i < txIds.Length; i++)
			{
				result[i] = AccessCoins(txIds[i]);
			}
			return result;
		}
	}
}
