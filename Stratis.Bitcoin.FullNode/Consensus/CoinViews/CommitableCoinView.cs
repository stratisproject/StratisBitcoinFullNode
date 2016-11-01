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

		//Prunable coins should be flushed down inner
		InMemoryCoinView _Uncommited = new InMemoryCoinView() { RemovePrunableCoins = false };

		bool update = false;

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
			if(uncommited != null)
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
			return coins;
		}

		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			update = true;
			_Uncommited.SaveChanges(newTip, txIds, coins);
		}

		public void Clear()
		{
			_Uncommited.Clear();
		}

		public void Commit()
		{
			if(!update)
				return;
			Inner.SaveChanges(_Uncommited.Tip, _Uncommited.coins.Keys, _Uncommited.coins.Values);
			update = false;
		}

		public void Commit(CoinView coinview)
		{
			coinview.SaveChanges(_Uncommited.Tip, _Uncommited.coins.Keys, _Uncommited.coins.Values);
		}

		public void Update(Transaction tx, int height)
		{
			update = true;
			_Uncommited.SaveChanges(tx, height);
		}
	}
}
