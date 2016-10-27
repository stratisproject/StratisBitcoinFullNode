using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.BitcoinCore;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	/// <summary>
	/// Wrap a CoinView doing IO, so that IO happens in background.
	/// </summary>
	public class BackgroundCommiterCoinView : CoinView
	{
		CoinView _Inner;
		CommitableCoinView _Commitable;
		CommitableCoinView _InnerCommitable;
		public BackgroundCommiterCoinView(CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Inner = inner;
			_InnerCommitable = new CommitableCoinView(_Inner);
			_Commitable = new CommitableCoinView(_InnerCommitable);
		}

		public override ChainedBlock Tip
		{
			get
			{
				return _Commitable.Tip;
			}
		}

		public ChainedBlock CommitingTip
		{
			get
			{
				return _InnerCommitable.Tip;
			}
		}

		public ChainedBlock InnerTip
		{
			get
			{
				return _Inner.Tip;
			}
		}

		public override Coins AccessCoins(uint256 txId)
		{
			return _Commitable.AccessCoins(txId);
		}

		Task _Commiting;
		public override void SaveChanges(ChainedBlock newTip, IEnumerable<uint256> txIds, IEnumerable<Coins> coins)
		{
			_Commitable.SaveChanges(newTip, txIds, coins);

			if((_Commiting == null || _Commiting.IsCompleted))
			{
				_Commitable.SaveChanges();
				_Commiting = Task.Run(() =>
				{
					_InnerCommitable.SaveChanges();
				});
			}
		}
	}
}
