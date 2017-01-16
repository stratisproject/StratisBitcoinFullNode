using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.BlockPulling
{
	public abstract class BlockPuller
	{
		public abstract void SetLocation(ChainedBlock location);

		public abstract Block NextBlock(CancellationToken cancellationToken);

		public abstract void RequestOptions(TransactionOptions transactionOptions);
	}
}
