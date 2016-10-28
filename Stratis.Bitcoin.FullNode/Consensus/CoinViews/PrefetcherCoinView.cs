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
		void PrefetchUTXOs(BlockHeader prefetchedHeader,  uint256[] txIds);
	}
	public class PrefetcherCoinView : CoinView, IPrefetcherCoinView, IBackedCoinView
	{
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

		public override Coins AccessCoins(uint256 txId)
		{
			if(_CurrentPrefetch == null)
				return _Inner.AccessCoins(txId);
			return _CurrentPrefetch.GetAwaiter().GetResult().AccessCoins(txId);
		}

		public override Coins[] FetchCoins(uint256[] txIds)
		{
			if(_CurrentPrefetch == null)
				return _Inner.FetchCoins(txIds);
			return _CurrentPrefetch.GetAwaiter().GetResult().FetchCoins(txIds);
		}

		static readonly Coins MissingCoins = new Coins();
		public void PrefetchUTXOs(BlockHeader prefetchedHeader, uint256[] txIds)
		{
			if(_PrefetchesByPrev.ContainsKey(prefetchedHeader.HashPrevBlock))
				return;
			var task = Task.Run(() =>
			{
				var inMemory = new InMemoryCoinView();				
				var coins = _Inner.FetchCoins(txIds);
				inMemory.SaveChanges(_Inner.Tip, txIds, coins);
				return inMemory;
			});
			_PrefetchesByPrev.Add(prefetchedHeader.HashPrevBlock, task);
		}

		Dictionary<uint256, Task<InMemoryCoinView>> _PrefetchesByPrev = new Dictionary<uint256, Task<InMemoryCoinView>>();
		Task<InMemoryCoinView> _CurrentPrefetch;

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			_CurrentPrefetch = _PrefetchesByPrev.TryGet(newTip.HashBlock);
			_PrefetchesByPrev.Remove(newTip.HashBlock);
			_Inner.SaveChanges(newTip, txIds, coins);
		}
	}
}
