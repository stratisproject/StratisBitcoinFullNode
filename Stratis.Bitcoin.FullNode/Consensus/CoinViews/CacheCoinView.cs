using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class CacheCoinView : CoinView, IBackedCoinView
	{
		HashSet<uint256> _NotFound = new HashSet<uint256>();
		public CacheCoinView(CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Inner = inner;
			_Cache = new InMemoryCoinView(_Inner.Tip) { RemovePrunableCoins = false };
			ReadThrough = true;
			WriteThrough = true;
			MaxItems = 100000;
		}

		public int MaxItems
		{
			get;
			set;
		}

		public bool ReadThrough
		{
			get;
			set;
		}

		public bool WriteThrough
		{
			get;
			set;
		}

		public bool Contains(uint256 txId)
		{
			return _Cache.AccessCoins(txId) != null || _NotFound.Contains(txId);
		}

		private readonly CoinView _Inner;
		private readonly InMemoryCoinView _Cache;

		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public override ChainedBlock Tip
		{
			get
			{
				return _Inner.Tip;
			}
		}


		private readonly CachePerformanceCounter _PerformanceCounter = new CachePerformanceCounter();
		public CachePerformanceCounter PerformanceCounter
		{
			get
			{
				return _PerformanceCounter;
			}
		}

		public override Coins AccessCoins(uint256 txId)
		{
			if(_NotFound.Contains(txId))
			{
				PerformanceCounter.AddHitCount(1);
				return null;
			}
			var cached = _Cache.AccessCoins(txId);
			if(cached != null)
			{
				PerformanceCounter.AddHitCount(1);
				return cached;
			}
			PerformanceCounter.AddMissCount(1);
			var coin = _Inner.AccessCoins(txId);
			if(ReadThrough)
			{
				AddToCache(txId, coin);
			}
			return coin;
		}

		public override Coins[] FetchCoins(uint256[] txIds)
		{
			var fetched = _Cache.FetchCoins(txIds);
			List<uint256> toFetch = new List<uint256>(txIds.Length);
			for(int i = 0; i < txIds.Length; i++)
			{
				if(fetched[i] == null && !_NotFound.Contains(txIds[i]))
				{
					toFetch.Add(txIds[i]);
				}
			}
			PerformanceCounter.AddMissCount(toFetch.Count);
			PerformanceCounter.AddHitCount(txIds.Length - toFetch.Count);

			var innerCoins = _Inner.FetchCoins(toFetch.ToArray());

			int innerIndex = 0;
			for(int i = 0; i < txIds.Length; i++)
			{
				if(fetched[i] == null && !_NotFound.Contains(txIds[i]))
				{
					toFetch.Add(txIds[i]);
					fetched[i] = innerCoins[innerIndex++];
					if(ReadThrough)
					{
						AddToCache(txIds[i], fetched[i]);
					}
				}
			}
			return fetched;
		}

		private void AddToCache(uint256 id, Coins coins)
		{
			if(coins == null)
			{
				_Cache.SaveChange(id, null);
				_NotFound.Add(id);
			}
			else
			{
				_Cache.SaveChange(id, coins);
				_NotFound.Remove(id);
			}
		}

		public int CacheEntryCount
		{
			get
			{
				return _Cache.coins.Count + _NotFound.Count;
			}
		}

		Random _Rand = new Random();
		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			if(WriteThrough)
			{
				var idEnum = txIds.GetEnumerator();
				var coinsEnum = coins.GetEnumerator();
				while(idEnum.MoveNext())
				{
					coinsEnum.MoveNext();
					AddToCache(idEnum.Current, coinsEnum.Current);
				}
			}
			if(CacheEntryCount > MaxItems)
			{
				Evict();
			}
			_Inner.SaveChanges(newTip, txIds, coins);
		}

		private void Evict()
		{
			List<uint256> toDelete = new List<uint256>();
			foreach(var item in _Cache.coins)
			{
				if(_Rand.Next() % 3 == 0)
					toDelete.Add(item.Key);
			}

			foreach(var delete in toDelete)
				_Cache.coins.Remove(delete);

			toDelete.Clear();
			foreach(var item in _NotFound)
			{
				if(_Rand.Next() % 3 == 0)
					toDelete.Add(item);
			}

			foreach(var delete in toDelete)
				_NotFound.Remove(delete);
		}
	}
}
