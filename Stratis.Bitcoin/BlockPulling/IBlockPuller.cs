using NBitcoin;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Puller that download blocks from peers.
    /// </summary>
    public interface IBlockPuller
    {
        /// <summary>
        /// Prepares and invokes download tasks from peer nodes for blocks the node is missing.
        /// </summary>
        /// <param name="downloadRequests">
        /// Array of block descriptions that need to be downloaded.
        /// Blocks in the array have to be unique - it is not supported for a single block to be included twice in this array.
        /// </param>
        void AskBlocks(ChainedBlock[] downloadRequests);

        /// <summary>
        /// Push a block to downloaded blocks with ability to cancel the operation using the cancellation token.
        /// </summary>
        /// <param name="length">Length of the serialized block in bytes.</param>
        /// <param name="block">Block to push.</param>
        /// <param name="token">Cancellation token to be used by derived classes that allows the caller to cancel the execution of the push operation.</param>
        void PushBlock(int length, Block block, CancellationToken token);

        /// <summary>
        /// Check whether a specific block identified by its header hash is currently being downloaded.
        /// </summary>
        /// <param name="hash">Hash of the block header.</param>
        /// <returns>true if the specific block is currently being downloaded, false otherwise.</returns>
        bool IsDownloading(uint256 hash);
    }
}
