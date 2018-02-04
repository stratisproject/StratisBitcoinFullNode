using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Puller that downloads blocks from peers.
    /// </summary>
    public class StoreBlockPuller : BlockPuller, IDisposable
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly AsyncQueue<DownloadedBlock> downloadedBlocks;

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes.
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public StoreBlockPuller(ConcurrentChain chain, Connection.IConnectionManager nodes, ILoggerFactory loggerFactory)
            : base(chain, nodes.ConnectedPeers, nodes.NodeSettings.ProtocolVersion, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.downloadedBlocks = new AsyncQueue<DownloadedBlock>();
        }

        /// <summary>
        /// Prepares and invokes a download task for multiple blocks.
        /// </summary>
        public void AskForMultipleBlocks(ChainedBlock[] downloadRequests)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(downloadRequests), nameof(downloadRequests.Length), downloadRequests.Length);

            this.AskBlocks(downloadRequests);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Decreases quality score of the peer that is supposed to provide specified block.</summary>
        public void Stall(ChainedBlock chainedBlock)
        {
            this.OnStalling(chainedBlock);
        }

        public async Task<DownloadedBlock> GetNextDownloadedBlockAsync(CancellationToken cancellation)
        {
            return await this.downloadedBlocks.DequeueAsync(cancellation);
        }

        public override void BlockPushed(uint256 blockHash, DownloadedBlock downloadedBlock, CancellationToken cancellationToken)
        {
            this.downloadedBlocks.Enqueue(downloadedBlock);
            base.BlockPushed(blockHash, downloadedBlock, cancellationToken);
        }

        public void Dispose()
        {
            this.downloadedBlocks.Dispose();
        }
    }
}