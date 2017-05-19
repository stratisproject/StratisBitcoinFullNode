using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using System.Diagnostics;
using System.Threading;

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

		private readonly ReaderWriterLock lockobj;
		private readonly Dictionary<uint256, CacheItem> unspents;
		private uint256 blockHash;
		private uint256 innerBlockHash;
		private readonly CoinView inner;
		private readonly StakeChainStore stakeChainStore;

		public CachedCoinView(DBreezeCoinView inner, StakeChainStore stakeChainStore = null)
		{
			Guard.NotNull(inner, nameof(inner));

			this.inner = inner;
			this.stakeChainStore = stakeChainStore;
			this.MaxItems = 100000;
			this.lockobj = new ReaderWriterLock();
			this.unspents = new Dictionary<uint256, CacheItem>();
			this.PerformanceCounter =  new CachePerformanceCounter();
		}

		/// <summary>
		/// This is used for testing the coin view
		/// it allows a coinview that only has in memory entries
		/// </summary>
		public CachedCoinView(InMemoryCoinView inner, StakeChainStore stakeChainStore = null)
		{
			Guard.NotNull(inner, nameof(inner));

			this.inner = inner;
			this.stakeChainStore = stakeChainStore;
			this.MaxItems = 100000;
			this.lockobj = new ReaderWriterLock();
			this.unspents = new Dictionary<uint256, CacheItem>();
			this.PerformanceCounter = new CachePerformanceCounter();
		}

		public CoinView Inner => inner;

		public override async Task<FetchCoinsResponse> FetchCoinsAsync(uint256[] txIds)
		{
			Guard.NotNull(txIds, nameof(txIds));
			
			FetchCoinsResponse result = null;
			UnspentOutputs[] outputs = new UnspentOutputs[txIds.Length];
			List<int> miss = new List<int>();
			List<uint256> missedTxIds = new List<uint256>();
			using(this.lockobj.LockRead())
			{
				WaitOngoingTasks();
				for(int i = 0; i < txIds.Length; i++)
				{
					CacheItem cache;
					if(!this.unspents.TryGetValue(txIds[i], out cache))
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
			using(this.lockobj.LockWrite())
			{
				this.flushing.Wait();
				var innerblockHash = fetchedCoins.BlockHash;
				if(blockHash == null)
				{
					Debug.Assert(this.unspents.Count == 0);
					this.innerBlockHash = innerblockHash;
					blockHash = innerBlockHash;
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
					this.unspents.TryAdd(txIds[index], cache);
				}
				result = new FetchCoinsResponse(outputs, blockHash);
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

		Task flushing = Task.CompletedTask;
		public async Task FlushAsync()
		{
			// before flushing the coinview persist the stake store
			// the stake store depends on the last block hash
			// to be stored after the stake store is persisted
			if (this.stakeChainStore != null)
				await this.stakeChainStore.Flush(true);

			if (innerBlockHash == null)
				innerBlockHash = await inner.GetBlockHashAsync().ConfigureAwait(false);

			using(this.lockobj.LockWrite())
			{
				WaitOngoingTasks();
				if(innerBlockHash == null)
					return;
				var unspent =
				unspents.Where(u => u.Value.IsDirty)
				.ToArray();

				var originalOutputs = unspent.Select(u => u.Value.OriginalOutputs).ToList();
				foreach(var u in unspent)
				{
					u.Value.IsDirty = false;
					u.Value.ExistInInner = true;
					u.Value.OriginalOutputs = u.Value.UnspentOutputs?._Outputs.ToArray();
				}
				this.flushing = Inner.SaveChangesAsync(unspent.Select(u => u.Value.UnspentOutputs).ToArray(), originalOutputs, innerBlockHash, blockHash);

				//Remove from cache prunable entries as they are being flushed down
				foreach(var c in unspent.Where(c => c.Value.UnspentOutputs != null && c.Value.UnspentOutputs.IsPrunable))
					unspents.Remove(c.Key);
				innerBlockHash = blockHash;
			}
			//Can't await inside a lock
			await this.flushing.ConfigureAwait(false);
		}

		private void Evict()
		{
			using(this.lockobj.LockWrite())
			{
				Random rand = new Random();
				foreach(var entry in this.unspents.ToList())
				{
					if(!entry.Value.IsDirty)
					{
						if(rand.Next() % 3 == 0)
							this.unspents.Remove(entry.Key);
					}
				}
			}
		}

		public int CacheEntryCount => this.unspents.Count;

		public int MaxItems
		{
			get; set;
		}

		public CachePerformanceCounter PerformanceCounter
		{
			get; set;
		}

		private static readonly uint256[] DuplicateTransactions = new[] { new uint256("e3bf3d07d4b0375638d5f1db5255fe07ba2c4cb067cd81b84ee974b6585fb468"), new uint256("d5d27987d2a3dfc724e359870c6644b40e497bdc0589a033220fe15429d88599") };
		public override Task SaveChangesAsync(IEnumerable<UnspentOutputs> unspentOutputs, IEnumerable<TxOut[]> originalOutputs, uint256 oldBlockHash, uint256 nextBlockHash)
		{
			Guard.NotNull(oldBlockHash, nameof(oldBlockHash));
			Guard.NotNull(nextBlockHash, nameof(nextBlockHash));
			Guard.NotNull(unspentOutputs, nameof(unspentOutputs));
			
			using(this.lockobj.LockWrite())
			{
				WaitOngoingTasks();
				if(blockHash != null && oldBlockHash != blockHash)
					return Task.FromException(new InvalidOperationException("Invalid oldBlockHash"));
				blockHash = nextBlockHash;
				foreach(var unspent in unspentOutputs)
				{
					CacheItem existing;
					if(this.unspents.TryGetValue(unspent.TransactionId, out existing))
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
						this.unspents.Add(unspent.TransactionId, existing);
					}
					existing.IsDirty = true;
					//Inner does not need to know pruned unspent that it never saw.
					if(existing.UnspentOutputs.IsPrunable && !existing.ExistInInner)
						this.unspents.Remove(unspent.TransactionId);
				}
				return Task.FromResult(true);
			}
		}

		Task rewinding = Task.CompletedTask;
		public override async Task<uint256> Rewind()
		{
			if(innerBlockHash == null)
				innerBlockHash = await inner.GetBlockHashAsync().ConfigureAwait(false);

			Task<uint256> rewindinginner = null;
			using(this.lockobj.LockWrite())
			{
				WaitOngoingTasks();
				if(blockHash == innerBlockHash)
					this.unspents.Clear();
				if(this.unspents.Count != 0)
				{
					//More intelligent version can restore without throwing away the cache. (as the rewind data is in the cache)
					this.unspents.Clear();
					blockHash = innerBlockHash;
					return blockHash;
				}
				else
				{
					rewindinginner = inner.Rewind();
					this.rewinding = rewindinginner;
				}
			}
			var h = await rewindinginner.ConfigureAwait(false);
			using(lockobj.LockWrite())
			{
				innerBlockHash = h;
				blockHash = h;
			}
			return h;
		}

		private void WaitOngoingTasks()
		{
			Task.WaitAll(this.flushing, this.rewinding);
		}
	}
}

