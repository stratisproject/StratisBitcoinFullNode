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
		void PrefetchUTXOs(Block block);
	}
	public class PrefetcherCoinView : CoinView, IPrefetcherCoinView
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

		public override Coins AccessCoins(uint256 txId)
		{
			return _Prefetch == null ? _Inner.AccessCoins(txId) : _Prefetch.AccessCoins(txId);
		}

		static readonly Coins MissingCoins = new Coins();
		public void PrefetchUTXOs(Block block)
		{
			if(_NextPrefetchTask != null)
				throw new InvalidOperationException("One prefetch at a time allowed");
			_NextPrefetch = new CommitableCoinView(_Inner);
			_NextPrefetch.CacheMissingCoins = true;
			var prefetch = _NextPrefetch;
			_NextPrefetchTask = Task.Run(() =>
			{
				int i = 0;
				uint256[] ids = new uint256[block.Transactions.Count + block.Transactions.Where(tx=>!tx.IsCoinBase).SelectMany(txin => txin.Inputs).Count()];
				foreach(var tx in block.Transactions)
				{
					ids[i++] = tx.GetHash();
					if(!tx.IsCoinBase)
						foreach(var input in tx.Inputs)
						{
							ids[i++] = input.PrevOut.Hash;
						}
				}
				prefetch.FetchCoins(ids);
			});
		}

		CommitableCoinView _NextPrefetch = null;
		Task _NextPrefetchTask = null;
		CommitableCoinView _Prefetch;
		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			_Prefetch = _NextPrefetch;
			if(_NextPrefetchTask != null)
			{
				_NextPrefetchTask.Wait();
				_NextPrefetchTask = null;
				_NextPrefetch.SaveChanges(newTip, txIds, coins);
				_NextPrefetch = null;
			}
			_Inner.SaveChanges(newTip, txIds, coins);
		}
	}
}
