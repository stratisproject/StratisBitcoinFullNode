using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public enum ThresholdState
	{
		Defined,
		Started,
		LockedIn,
		Failed,
		Active
	}
}
