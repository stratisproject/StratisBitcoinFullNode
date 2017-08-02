using NBitcoin;
using System.Threading;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

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
        /// Inject blocks directly to the puller's list of downloaded blocks, which is used for testing.
        /// </summary>
        /// <param name="blockHash">Hash of the block to inject.</param>
        /// <param name="downloadedBlock">Desciption of the inject block as if it was downloaded.</param>
        /// <param name="cancellationToken">Cancellation token to allow the caller to cancel the execution of the operation.</param>
        void InjectBlock(uint256 blockHash, DownloadedBlock downloadedBlock, CancellationToken cancellationToken);

        /// <summary>
        /// Check whether a specific block identified by its header hash is currently being downloaded.
        /// </summary>
        /// <param name="hash">Hash of the block header.</param>
        /// <returns>true if the specific block is currently being downloaded, false otherwise.</returns>
        bool IsDownloading(uint256 hash);
    }
}
