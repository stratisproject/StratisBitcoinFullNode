using NBitcoin;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
	public interface IBlockPuller
	{
		void SetLocation(ChainedBlock location);

		Block NextBlock(CancellationToken cancellationToken);

		void RequestOptions(TransactionOptions transactionOptions);
	}
}
