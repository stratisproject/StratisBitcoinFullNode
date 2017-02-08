using NBitcoin;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
	public abstract class BlockPuller
	{
		public abstract void SetLocation(ChainedBlock location);

		public abstract Block NextBlock(CancellationToken cancellationToken);

		public abstract void RequestOptions(TransactionOptions transactionOptions);
	}
}
