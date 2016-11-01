using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class CommitableCoinView : CoinView, IBackedCoinView
	{
		CoinView _Inner;
		HashSet<uint256> _ChangedCoins = new HashSet<uint256>();
		//Prunable coins should be flushed down inner
		InMemoryCoinView _Uncommited = new InMemoryCoinView() { RemovePrunableCoins = false };

		public override ChainedBlock Tip
		{
			get
			{
				return _Uncommited.Tip;
			}
		}

		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public bool NoInnerQuery
		{
			get;
			set;
		}

		static IEnumerable<uint256> NullUItn256s = new uint256[0];
		static IEnumerable<Coins> NullCoins = new Coins[0];
		public CommitableCoinView(ChainedBlock newTip, CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			if(newTip == null)
				throw new ArgumentNullException("newTip");
			_Inner = inner;
			_Uncommited.SaveChanges(newTip, NullUItn256s, NullCoins);
		}
		public CommitableCoinView(CoinView inner) : this(inner.Tip, inner)
		{
		}

		public override Coins AccessCoins(uint256 txId)
		{
			var uncommited = _Uncommited.AccessCoins(txId);
			if(NoInnerQuery || uncommited != null)
				return uncommited;
			var coin = Inner.AccessCoins(txId);
			_Uncommited.SaveChange(txId, coin);
			return coin;
		}

		public override Coins[] FetchCoins(uint256[] txIds)
		{
			Coins[] coins = new Coins[txIds.Length];
			int i = 0;
			int notInCache = 0;
			foreach(var coin in _Uncommited.FetchCoins(txIds))
			{
				if(coin == null)
					notInCache++;
				coins[i++] = coin;
			}

			uint256[] txIds2 = new uint256[notInCache];
			i = 0;
			for(int ii = 0; ii < txIds.Length; ii++)
			{
				if(coins[ii] == null)
					txIds2[i++] = txIds[ii];
			}

			i = 0;

			if(!NoInnerQuery)
			{
				foreach(var coin in Inner.FetchCoins(txIds2))
				{
					for(; i < coins.Length;)
					{
						if(coins[i] == null)
							break;
						i++;
					}
					if(i >= coins.Length)
						break;
					_Uncommited.SaveChange(txIds[i], coin);
					coins[i] = coin;
					i++;
				}
			}
			return coins;
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			foreach(var id in txIds)
				_ChangedCoins.Add(id);
			_Uncommited.SaveChanges(newTip, txIds, coins);
		}
		public void Update(Transaction tx, int height)
		{
			_ChangedCoins.Add(tx.GetHash());
			_Uncommited.SaveChanges(tx, height);
		}

		public void Clear()
		{
			_Uncommited.Clear();
			_ChangedCoins.Clear();
		}

		public void Commit()
		{
			var changedCoins = GetChangedCoins();
			Inner.SaveChanges(_Uncommited.Tip, changedCoins.Keys, changedCoins.Values);
		}

		private Dictionary<uint256, Coins> GetChangedCoins()
		{
			var changed = new Dictionary<uint256, Coins>(_ChangedCoins.Count);
			foreach(var kv in _Uncommited.coins)
			{
				if(_ChangedCoins.Contains(kv.Key))
					changed.Add(kv.Key, kv.Value);
			}
			return changed;
		}

		public void Commit(CoinView coinview)
		{
			var changedCoins = GetChangedCoins();
			coinview.SaveChanges(_Uncommited.Tip, changedCoins.Keys, changedCoins.Values);
		}
	}
}
