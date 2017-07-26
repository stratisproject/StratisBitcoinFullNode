using NBitcoin;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Puller that download blocks from peers.
    /// </summary>
    public class StoreBlockPuller : BlockPuller
    {
        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        public StoreBlockPuller(ConcurrentChain chain, Connection.IConnectionManager nodes)
            : base(chain, nodes.ConnectedNodes, nodes.NodeSettings.ProtocolVersion)
        {
        }

        /// <summary>
        /// Prepares and invokes download task for a single block.
        /// </summary>
        /// <param name="downloadRequest">Description of a block to download.</param>
        public void AskBlock(ChainedBlock downloadRequest)
        {
            base.AskBlocks(new ChainedBlock[] { downloadRequest });
        }

        /// <summary>
        /// Tries to retrieve a specific downloaded block from the list of downloaded blocks.
        /// </summary>
        /// <param name="chainedBlock">Header of the block to retrieve.</param>
        /// <param name="block">If the function succeeds, the downloaded block is returned in this parameter.</param>
        /// <returns>true if the function succeeds, false otherwise.</returns>
        public bool TryGetBlock(ChainedBlock chainedBlock, out DownloadedBlock block)
        {
            if (this.DownloadedBlocks.TryRemove(chainedBlock.HashBlock, out block))
            {
                return true;
            }

            this.OnStalling(chainedBlock);
            return false;
        }
    }
}