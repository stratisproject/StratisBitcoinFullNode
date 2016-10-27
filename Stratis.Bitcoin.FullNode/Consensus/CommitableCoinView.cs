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
		}
		public CommitableCoinView(CoinView inner) : this(inner.Tip, inner)
		{
		}

		public override Coins AccessCoins(uint256 txId)
		{
			return AccessCoins(txId, true);
		}
		public Coins AccessCoins(uint256 txId, bool accessInner)
		{
			var cachedCoin = _Cache.AccessCoins(txId);
			if(cachedCoin != null || !accessInner)
				return cachedCoin;
			var coin = _Inner.AccessCoins(txId);
			if(coin == null)
				return null;
			coin = coin.Clone();
			_Cache.SaveChange(txId, coin);
			return coin;
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

		public void Update(Transaction tx, int height)
		{
			update = true;
			_Cache.SaveChanges(tx, height);
		}
	}
}
