using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System.Diagnostics;

namespace Stratis.Bitcoin.Consensus
{
	public class CachedCoinView : CoinView, IBackedCoinView
	{
		class CacheItem
		{
			public UnspentOutputs UnspentOutputs;
			public bool ExistInInner;
			public bool IsDirty;
			public TxOut[] OriginalOutputs;
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
			UnspentOutputs[] outputs = new UnspentOutputs[txIds.Length];
			List<int> miss = new List<int>();
			List<uint256> missedTxIds = new List<uint256>();
			using(_Lock.LockRead())
			{
				WaitOngoingTasks();
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
						outputs[i] = cache.UnspentOutputs == null ? null :
									 cache.UnspentOutputs.IsPrunable ? null :
									 cache.UnspentOutputs.Clone();
					}
				}
				PerformanceCounter.AddMissCount(miss.Count);
				PerformanceCounter.AddHitCount(txIds.Length - miss.Count);
			}
			var fetchedCoins = await Inner.FetchCoinsAsync(missedTxIds.ToArray()).ConfigureAwait(false);
			using(_Lock.LockWrite())
			{
				_Flushing.Wait();
				innerBlockHash = fetchedCoins.BlockHash;
				if(_BlockHash == null)
				{
					Debug.Assert(_Unspents.Count == 0);
					_InnerBlockHash = innerBlockHash;
					_BlockHash = innerBlockHash;
				}
				for(int i = 0; i < miss.Count; i++)
				{
					var index = miss[i];
					var unspent = fetchedCoins.UnspentOutputs[i];
					outputs[index] = unspent;
					CacheItem cache = new CacheItem();
					cache.ExistInInner = unspent != null;
					cache.IsDirty = false;
					cache.UnspentOutputs = unspent;
					cache.OriginalOutputs = unspent?._Outputs.ToArray();
					_Unspents.TryAdd(txIds[index], cache);
				}
				result = new FetchCoinsResponse(outputs, _BlockHash);
			}

			if(CacheEntryCount > MaxItems)
			{
				Evict();
				if(CacheEntryCount > MaxItems)
				{
					await FlushAsync().ConfigureAwait(false);
					Evict();
				}

			}

			return result;
		}

		Task _Flushing = Task.CompletedTask;
		public async Task FlushAsync()
		{
			//wait previous flushing to complete
			await _Flushing.ConfigureAwait(false);

			if(_InnerBlockHash == null)
				_InnerBlockHash = await _Inner.GetBlockHashAsync().ConfigureAwait(false);

			KeyValuePair<uint256, CacheItem>[] unspent = null;
			using(_Lock.LockWrite())
			{
				if(_InnerBlockHash == null)
					return;
				unspent =
				_Unspents.Where(u => u.Value.IsDirty)
				.ToArray();

				foreach(var u in unspent)
				{
					u.Value.IsDirty = false;
					u.Value.ExistInInner = true;
				}
				_Flushing = Inner.SaveChangesAsync(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), unspent.Select(u => u.Value.OriginalOutputs), _InnerBlockHash, _BlockHash);

				//Remove from cache prunable entries as they are being flushed down
				foreach(var c in unspent.Where(c => c.Value.UnspentOutputs != null && c.Value.UnspentOutputs.IsPrunable))
					_Unspents.Remove(c.Key);
				_InnerBlockHash = _BlockHash;
			}
			//Can't await inside a lock
			await _Flushing.ConfigureAwait(false);
		}

		private void Evict()
		{
			using(_Lock.LockWrite())
			{
				Random rand = new Random();
				foreach(var entry in _Unspents.ToList())
				{
					if(!entry.Value.IsDirty)
					{
						if(rand.Next() % 3 == 0)
							_Unspents.Remove(entry.Key);
					}
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

		static uint256[] DuplicateTransactions = new[] { new uint256("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468"), new uint256("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599") };
		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			if(oldBlockHash == null)
				throw new ArgumentNullException("oldBlockHash");
			if(nextBlockHash == null)
				throw new ArgumentNullException("nextBlockHash");
			if(unspentOutputs == null)
				throw new ArgumentNullException("unspentOutputs");

			using(_Lock.LockWrite())
			{
				WaitOngoingTasks();
				if(_BlockHash != null && oldBlockHash != _BlockHash)
					return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));
				_BlockHash = nextBlockHash;
				foreach(var unspent in unspentOutputs)
				{
					CacheItem existing;
					if(_Unspents.TryGetValue(unspent.TransactionId, out existing))
					{
						if(existing.UnspentOutputs != null)
							existing.UnspentOutputs.Spend(unspent);
						else
							existing.UnspentOutputs = unspent;
					}
					else
					{
						existing = new CacheItem();
						existing.ExistInInner = !unspent.IsFull; //seems to be a new created coin (careful, untrue if rewinding)
						existing.ExistInInner |= DuplicateTransactions.Any(t => unspent.TransactionId == t);
						existing.IsDirty = true;
						existing.UnspentOutputs = unspent;
						_Unspents.Add(unspent.TransactionId, existing);
					}
					existing.IsDirty = true;
					//Inner does not need to know pruned unspent that it never saw.
					if(existing.UnspentOutputs.IsPrunable && !existing.ExistInInner)
						_Unspents.Remove(unspent.TransactionId);
				}
				return Task.FromResult(true);
			}
		}

		Task _Rewinding = Task.CompletedTask;
		public override async Task<uint256> Rewind()
		{
			if(_InnerBlockHash == null)
				_InnerBlockHash = await _Inner.GetBlockHashAsync().ConfigureAwait(false);

			var innerHash = _InnerBlockHash;
			Task<uint256> rewinding = null;
			using(_Lock.LockWrite())
			{
				WaitOngoingTasks();
				if(_Unspents.Count != 0)
				{
					//More intelligent version can restore without throwing away the cache. (as the rewind data is in the cache)
					_Unspents.Clear();
					_BlockHash = _InnerBlockHash;
					return _BlockHash;
				}
				else
				{
					rewinding = _Inner.Rewind();
					_Rewinding = rewinding;
				}
			}
			var h = await rewinding.ConfigureAwait(false);
			using(_Lock.LockWrite())
			{
				_InnerBlockHash = h;
				_BlockHash = h;
			}
			return h;
		}

		private void WaitOngoingTasks()
		{
			Task.WaitAll(_Flushing, _Rewinding);
		}
	}
}

