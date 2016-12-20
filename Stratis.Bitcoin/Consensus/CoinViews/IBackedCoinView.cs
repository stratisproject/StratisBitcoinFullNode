using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
	public interface IBackedCoinView
	{
		CoinView Inner
		{
			get;
		}
	}
}
