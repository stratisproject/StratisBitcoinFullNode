using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;

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
        /// Prepares and invokes download task for a single block.
        /// </summary>
        /// <param name="downloadRequest">Description of a block to download.</param>
        public void AskBlock(ChainedBlock downloadRequest)
        {
            this.logger.LogTrace($"({nameof(downloadRequest)}:'{downloadRequest.HashBlock}/{downloadRequest.Height}')");

            base.AskBlocks(new ChainedBlock[] { downloadRequest });

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
            this.logger.LogTrace($"({nameof(chainedBlock)}:'{chainedBlock.HashBlock}/{chainedBlock.Height}')");

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
