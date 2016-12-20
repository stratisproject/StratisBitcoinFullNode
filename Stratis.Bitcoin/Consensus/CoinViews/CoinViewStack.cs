using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public class CoinViewStack
	{
		public CoinViewStack(CoinView top)
		{
			Top = top;
			var current = top;
			while(current is IBackedCoinView)
			{
				current = ((IBackedCoinView)current).Inner;
			}
			Bottom = current;
		}

		public IEnumerable<CoinView> GetElements()
		{
			var current = Top;
			while(current is IBackedCoinView)
			{
				yield return current;
				current = ((IBackedCoinView)current).Inner;
			}
			yield return current;
		}

		public T Find<T>()
		{
			var current = Top;
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
