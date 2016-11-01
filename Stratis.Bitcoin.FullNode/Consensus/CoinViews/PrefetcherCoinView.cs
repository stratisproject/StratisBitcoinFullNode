using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;
using System.Diagnostics;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public interface IPrefetcherCoinView
	{
		void PrefetchUTXOs(BlockHeader prefetchedHeader, uint256[] txIds);
	}
	public class PrefetcherCoinView : CoinView, IPrefetcherCoinView, IBackedCoinView
	{
		class Prefetching
		{
			public Task<InMemoryCoinView> Task;
			public ChainedBlock Date;
		}
		CoinView _Inner;
		public PrefetcherCoinView(CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Inner = inner;
		}
		public override ChainedBlock Tip
		{
			get
			{
				return _Inner.Tip;
			}
		}

		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public bool IsLoaded
		{
			get
			{
				return _CurrentPrefetch != null && _CurrentPrefetch.Task.Status == TaskStatus.RanToCompletion;
			}
		}

		public override Coins AccessCoins(uint256 txId)
		{
			if(_CurrentPrefetch == null)
				return _Inner.AccessCoins(txId);
			FlushChanges(_CurrentPrefetch);
			return _CurrentPrefetch.Task.GetAwaiter().GetResult().AccessCoins(txId);
		}

		public override Coins[] FetchCoins(uint256[] txIds)
		{
			if(_CurrentPrefetch == null)
				return _Inner.FetchCoins(txIds);
			FlushChanges(_CurrentPrefetch);
			return _CurrentPrefetch.Task.GetAwaiter().GetResult().FetchCoins(txIds);
		}

		static readonly Coins MissingCoins = new Coins();
		public void PrefetchUTXOs(BlockHeader prefetchedHeader, uint256[] txIds)
		{
			if(_PrefetchesByPrev.ContainsKey(prefetchedHeader.HashPrevBlock))
				return;
			Prefetching prefetching = new Prefetching();
			prefetching.Date = Tip;
			prefetching.Task = Task.Run(() =>
			{
				var inMemory = new InMemoryCoinView();
				inMemory.RemovePrunableCoins = false;
				var coins = _Inner.FetchCoins(txIds);
				inMemory.SaveChanges(_Inner.Tip, txIds, coins);
				inMemory.SpendOnly = true;
				return inMemory;
			});
			_PrefetchesByPrev.Add(prefetchedHeader.HashPrevBlock, prefetching);
		}

		Dictionary<uint256, Prefetching> _PrefetchesByPrev = new Dictionary<uint256, Prefetching>();
		Prefetching _CurrentPrefetch;
		List<CommitableCoinView> _LastChanges = new List<CommitableCoinView>();


		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{			
			CommitableCoinView changes = new CommitableCoinView(this);
			changes.SaveChanges(newTip, txIds, coins);
			_LastChanges.Add(changes);
			FlushChanges();
			CleanCommitables();
			_CurrentPrefetch = _PrefetchesByPrev.TryGet(newTip.HashBlock);
			_PrefetchesByPrev.Remove(newTip.HashBlock);
			_Inner.SaveChanges(newTip, txIds, coins);
		}

		private void FlushChanges()
		{
			foreach(var prefetch in _PrefetchesByPrev.Where(p => p.Value.Task.IsCompleted))
			{
				FlushChanges(prefetch.Value);
			}
		}

		private void FlushChanges(Prefetching prefetch)
		{
			foreach(var commitable in _LastChanges.OrderBy(l => l.Tip.Height))
			{
				if(prefetch.Date.Height < commitable.Tip.Height)
					commitable.Commit(prefetch.Task.GetAwaiter().GetResult());
				prefetch.Date = commitable.Tip;
			}
		}

		void CleanCommitables()
		{
			foreach(var commitable in _LastChanges.ToList())
			{
				if(_PrefetchesByPrev.Select(p => p.Value).All(p => p.Date.Height >= commitable.Tip.Height))
				{
					_LastChanges.Remove(commitable);
				}
			}
		}

		public void Wait()
		{
			Task.WaitAll(_PrefetchesByPrev.Select(t => t.Value.Task).ToArray());
			if(_CurrentPrefetch != null)
				_CurrentPrefetch.Task.Wait();
		}
	}
}
