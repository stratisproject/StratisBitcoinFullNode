using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.BlockPulling
{
	public abstract class BlockPuller
	{
		public abstract void SetLocation(ChainedBlock location);

		public abstract Block NextBlock();

		public abstract void Reject(Block block, RejectionMode rejectionMode);
	}
}
