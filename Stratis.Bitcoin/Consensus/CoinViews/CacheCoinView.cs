using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.Consensus
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

		class ToFetch
		{
			public int Index;
			public uint256 TxId;
		}
		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			var fetched = _Cache.FetchCoins(txIds);
			List<ToFetch> toFetch = new List<ToFetch>(txIds.Length);
			for(int i = 0; i < txIds.Length; i++)
			{
				if(fetched[i] == null && !_NotFound.Contains(txIds[i]))
				{
					toFetch.Add(new ToFetch() { TxId = txIds[i], Index = i });
				}
			}
			PerformanceCounter.AddMissCount(toFetch.Count);
			PerformanceCounter.AddHitCount(txIds.Length - toFetch.Count);

			var innerCoins = _Inner.FetchCoins(toFetch.Select(f => f.TxId).ToArray());
			for(int i = 0; i < innerCoins.Length; i++)
			{
				if(ReadThrough)
				{
					AddToCache(toFetch[i].TxId, innerCoins[i]);
				}
				fetched[toFetch[i].Index] = innerCoins[i];
			}
			return fetched;
		}

		private void AddToCache(uint256 txId, UnspentOutputs coins)
		{
			if(coins == null)
			{
				_Cache.SaveChange(txId, null);
				_NotFound.Add(txId);
			}
			else
			{
				_Cache.SaveChange(coins.TransactionId, coins);
				_NotFound.Remove(coins.TransactionId);
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
		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			if(WriteThrough)
			{
				foreach(var output in unspentOutputs)
				{
					AddToCache(output.TransactionId, output);
				}
			}
			if(CacheEntryCount > MaxItems)
			{
				Evict();
			}
			_Inner.SaveChanges(newTip, unspentOutputs);
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
