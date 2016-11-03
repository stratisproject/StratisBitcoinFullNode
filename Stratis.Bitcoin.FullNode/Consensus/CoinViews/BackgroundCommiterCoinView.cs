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
	public class BackgroundCommiterCoinView : CoinView, IBackedCoinView
	{
		CoinView _Inner;
		CommitableCoinView _Commitable;
		CommitableCoinView _InnerCommitable;
		public BackgroundCommiterCoinView(CoinView inner)
		{
			if(inner == null)
				throw new ArgumentNullException("inner");
			_Inner = inner;
			_InnerCommitable = new CommitableCoinView(Inner);
			_InnerCommitable.ReadThrough = false;
			_Commitable = new CommitableCoinView(_InnerCommitable);
			FlushPeriod = TimeSpan.FromSeconds(0);
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
				return Inner.Tip;
			}
		}

		public override UnspentOutputs[] FetchCoins(uint256[] txIds)
		{
			return _Commitable.FetchCoins(txIds);
		}

		public TimeSpan FlushPeriod
		{
			get;
			set;
		}

		public CoinView Inner
		{
			get
			{
				return _Inner;
			}
		}

		public void Flush()
		{
			if(_Commiting != null)
				_Commiting.Wait();
			FlushAsync().Wait();
		}

		Task _Commiting;
		DateTimeOffset _LastFlush = DateTimeOffset.UtcNow;
		public override void SaveChanges(ChainedBlock newTip, IEnumerable<UnspentOutputs> unspentOutputs)
		{
			_Commitable.SaveChanges(newTip, unspentOutputs);
			if((_Commiting == null || _Commiting.IsCompleted) && (DateTimeOffset.UtcNow - _LastFlush) > FlushPeriod)
			{
				FlushAsync();
			}
		}

		private Task FlushAsync()
		{
			_InnerCommitable.Clear();
			_Commitable.Commit();
			_Commitable.Clear();
			var t = Task.Run(() =>
			{
				_InnerCommitable.Commit();
				_LastFlush = DateTimeOffset.UtcNow;
			});
			_Commiting = t;
			return t;
		}
	}
}
