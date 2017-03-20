using NBitcoin;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
	public interface IBlockPuller
	{
		void AskBlocks(ChainedBlock[] downloadRequests);

		void PushBlock(int length, Block block, CancellationToken token);

		bool IsDownloading(uint256 hash);
	}
}
