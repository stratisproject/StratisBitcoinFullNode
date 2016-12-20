using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class CachedCoinView : CoinView, IBackedCoinView
	{
		class CacheItem
		{
			public UnspentOutputs UnspentOutputs;
			public bool ExistInInner;
			public bool Synchronized;
		}

		ReaderWriterLock _Lock = new ReaderWriterLock();
		Dictionary<uint256, CacheItem> _Unspents = new Dictionary<uint256, CacheItem>();
		uint256 _BlockHash;
		uint256 _InnerBlockHash;
		CoinView _Inner;

		public CachedCoinView(CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Inner = inner;
			MaxItems = 100000;
		}

		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public override async Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
		{
			if(txIds == null)
				throw new ArgumentNullException("txIds");

			FetchCoinsResponse result = null;
			uint256 innerBlockHash = null;
			using(_Lock.LockRead())
			{
				UnspentOutputs[] outputs = new UnspentOutputs[txIds.Length];
				List<int> miss = new List<int>();
				List<uint256> missedTxIds = new List<uint256>();
				for(int i = 0; i < txIds.Length; i++)
				{
					CacheItem cache;
					if(!_Unspents.TryGetValue(txIds[i], out cache))
					{
						miss.Add(i);
						missedTxIds.Add(txIds[i]);
					}
					else
					{
						outputs[i] = cache.UnspentOutputs.IsPrunable ? null : cache.UnspentOutputs;
					}
				}
				PerformanceCounter.AddMissCount(miss.Count);
				PerformanceCounter.AddHitCount(txIds.Length - miss.Count);

				var fetchedCoins = await Inner.FetchCoinsAsync(missedTxIds.ToArray()).ConfigureAwait(false);
				innerBlockHash = fetchedCoins.BlockHash;


				for(int i = 0; i < miss.Count; i++)
				{
					var index = miss[i];
					var unspent = fetchedCoins.UnspentOutputs[i];
					outputs[index] = unspent;
					CacheItem cache = new CacheItem();
					cache.ExistInInner = unspent != null;
					cache.Synchronized = true;
					cache.UnspentOutputs = unspent;
					_Unspents.Add(unspent.TransactionId, cache);
				}

				result = new FetchCoinsResponse(outputs, _BlockHash);
			}

			if(innerBlockHash != _InnerBlockHash)
			{
				using(_Lock.LockWrite())
				{
					_InnerBlockHash = innerBlockHash;
				}
			}

			if(CacheEntryCount > MaxItems)
			{
				using(_Lock.LockWrite())
				{
					Evict();
					if(CacheEntryCount > MaxItems)
					{
						await FlushAsyncNoLock().ConfigureAwait(false);
						Evict();
					}
				}
			}

			return result;
		}

		public Task FlushAsync()
		{
			using(_Lock.LockWrite())
			{
				return FlushAsyncNoLock();
			}
		}

		private async Task FlushAsyncNoLock()
		{
			var unspent =
				_Unspents.Where(u => !u.Value.Synchronized)
				.Select(u => u.Value.UnspentOutputs)
				.ToArray();

			await Inner.SaveChangesAsync(unspent, _InnerBlockHash, _BlockHash).ConfigureAwait(false);
			_InnerBlockHash = _BlockHash;

			foreach(var u in _Unspents)
			{
				u.Value.Synchronized = true;
			}
		}

		private void Evict()
		{
			Random rand = new Random();
			foreach(var entry in _Unspents.ToList())
			{
				if(entry.Value.Synchronized)
				{
					if(rand.Next() % 3 == 0)
						_Unspents.Remove(entry.Key);
				}
			}
		}

		public int CacheEntryCount
		{
			get
			{
				return _Unspents.Count;
			}
		}

		public int MaxItems
		{
			get; set;
		}

		public CachePerformanceCounter PerformanceCounter
		{
			get; set;
		} = new CachePerformanceCounter();

		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			if(oldBlockHash == null)
				throw new ArgumentNullException("oldBlockHash");
			if(nextBlockHash == null)
				throw new ArgumentNullException("nextBlockHash");
			if(unspentOutputs == null)
				throw new ArgumentNullException("unspentOutputs");

			using(_Lock.LockWrite())
			{
				if(_BlockHash != null && oldBlockHash != _BlockHash)
					return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));
				_BlockHash = nextBlockHash;
				foreach(var unspent in unspentOutputs)
				{
					CacheItem existing;
					if(_Unspents.TryGetValue(unspent.TransactionId, out existing))
					{
						if(existing.UnspentOutputs != null)
							existing.UnspentOutputs.MergeFrom(unspent);
						else
							existing.UnspentOutputs = unspent.Clone();
					}
					else
					{
						existing = new CacheItem();
						existing.ExistInInner = false;
						existing.Synchronized = false;
						existing.UnspentOutputs = unspent.Clone();
						_Unspents.Add(unspent.TransactionId, existing);
					}
					existing.Synchronized = false;
					//Inner does not need to know pruned unspent that it never saw.
					if(existing.UnspentOutputs.IsPrunable && !existing.ExistInInner)
						_Unspents.Remove(unspent.TransactionId);
				}
				return Task.FromResult(true);
			}
		}
	}
}
