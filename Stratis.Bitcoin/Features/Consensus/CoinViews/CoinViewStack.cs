using System.Collections.Generic;

namespace Stratis.Bitcoin.Features.Consensus.CoinViews
{
	public class CoinViewStack
	{
		public CoinViewStack(CoinView top)
		{
            this.Top = top;
			var current = top;
			while(current is IBackedCoinView)
			{
				current = ((IBackedCoinView)current).Inner;
			}
            this.Bottom = current;
		}

		public IEnumerable<CoinView> GetElements()
		{
			var current = this.Top;
			while(current is IBackedCoinView)
			{
				yield return current;
				current = ((IBackedCoinView)current).Inner;
			}
			yield return current;
		}

		public T Find<T>()
		{
			var current = this.Top;
			if(current is T)
				return (T)(object)current;
			while(current is IBackedCoinView)
			{
				current = ((IBackedCoinView)current).Inner;
				if(current is T)
					return (T)(object)current;
			}
			return default(T);
		}

		public CoinView Top
		{
			get;
			private set;
		}

		public CoinView Bottom
		{
			get;
			private set;
		}
	}
}
