using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class CommitableCoinView : CoinView
	{
		CoinView _Inner;

		//Prunable coins should be flushed down inner
		InMemoryCoinView _Cache = new InMemoryCoinView() { RemovePrunableCoins = false };

		bool update = false;

		public override ChainedBlock Tip
		{
			get
			{
				return _Cache.Tip;
			}
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
			_Cache.SaveChanges(newTip, NullUItn256s, NullCoins);
			ReadThroughCache = true;
		}
		public CommitableCoinView(CoinView inner) : this(inner.Tip, inner)
		{
			ReadThroughCache = true;
		}

		public bool CacheMissingCoins
		{
			get;
			set;
		}

		public bool ReadThroughCache
		{
			get;
			set;
		}

		static readonly Coins MissingCoins = new Coins();
		public override Coins AccessCoins(uint256 txId)
		{
			return AccessCoins(txId, true);
		}
		public Coins AccessCoins(uint256 txId, bool accessInner)
		{
			var cachedCoin = _Cache.AccessCoins(txId);
			if(cachedCoin != null || !accessInner)
				return MissingCoins == cachedCoin ? null : cachedCoin;
			var coin = _Inner.AccessCoins(txId);
			if(coin == null)
			{
				if(ReadThroughCache && CacheMissingCoins)
					_Cache.SaveChange(txId, MissingCoins);
				return null;
			}
			coin = coin.Clone();
			if(ReadThroughCache)
				_Cache.SaveChange(txId, coin);
			return coin;
		}

		public override Coins[] FetchCoins(uint256[] txIds)
		{
			Coins[] coins = new Coins[txIds.Length];
			int i = 0;
			int notInCache = 0;
			foreach(var coin in _Cache.FetchCoins(txIds))
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
			foreach(var coin in _Inner.FetchCoins(txIds2))
			{
				for(; i < coins.Length;)
				{
					if(coins[i] == null)
						break;
					i++;
				}
				if(coin == null)
				{
					if(ReadThroughCache && CacheMissingCoins)
						_Cache.SaveChange(txIds[i], MissingCoins);
					coins[i++] = null;
					continue;
				}
				var cc = coin.Clone();
				if(ReadThroughCache)
					_Cache.SaveChange(txIds[i], cc);
				coins[i++] = cc;
			}
			return coins;
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			update = true;
			_Cache.SaveChanges(newTip, txIds, coins);
		}

		public void Clear()
		{
			_Cache.Clear();
		}

		public void SaveChanges()
		{
			if(!update)
				return;
			_Inner.SaveChanges(_Cache.Tip, _Cache.coins.Keys, _Cache.coins.Values);
			update = false;
		}

		public void SaveChanges(CoinView coinview)
		{
			coinview.SaveChanges(_Cache.Tip, _Cache.coins.Keys, _Cache.coins.Values);
		}

		public void Update(Transaction tx, int height)
		{
			update = true;
			_Cache.SaveChanges(tx, height);
		}
	}
}
