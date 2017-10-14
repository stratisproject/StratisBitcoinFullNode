using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Puller that download blocks from peers.
    /// </summary>
    public class StoreBlockPuller : BlockPuller
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public StoreBlockPuller(ConcurrentChain chain, Connection.IConnectionManager nodes, ILoggerFactory loggerFactory)
            : base(chain, nodes.ConnectedNodes, nodes.NodeSettings.ProtocolVersion, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Prepares and invokes a download task for multiple blocks.
        /// </summary>
        public void AskForMultipleBlocks(ChainedBlock[] downloadRequests)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(downloadRequests), nameof(downloadRequests.Length), downloadRequests.Length);

            base.AskBlocks(downloadRequests);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Tries to retrieve a specific downloaded block from the list of downloaded blocks.
        /// </summary>
        /// <param name="chainedBlock">Header of the block to retrieve.</param>
        /// <param name="block">If the function succeeds, the downloaded block is returned in this parameter.</param>
        /// <returns>true if the function succeeds, false otherwise.</returns>
        public bool TryGetBlock(ChainedBlock chainedBlock, out DownloadedBlock block)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(chainedBlock), chainedBlock);

            if (TryRemoveDownloadedBlock(chainedBlock.HashBlock, out block))
            {
                this.logger.LogTrace("(-):true");
                return true;
            }

            this.OnStalling(chainedBlock);
            this.logger.LogTrace("(-):false");
            return false;
        }
    }
}