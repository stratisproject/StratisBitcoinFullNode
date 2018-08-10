using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public sealed class PeerDisconnectedVisitor : IConsensusVisitor
    {
        private readonly ILogger logger;

        public int PeerId { get; set; }

        public PeerDisconnectedVisitor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        public ConsensusVisitorResult Visit(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1})", nameof(this.PeerId), this.PeerId);

            lock (consensusManager.PeerLock)
            {
                this.PeerDisconnectedLocked(consensusManager);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Called after a peer was disconnected.
        /// Informs underlying components about the even.
        /// Processes any remaining blocks to download.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="PeerLock"/>.</remarks>
        private void PeerDisconnectedLocked(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1})", nameof(this.PeerId), this.PeerId);

            bool removed = consensusManager.PeersByPeerId.Remove(this.PeerId);

            if (removed)
            {
                consensusManager.ChainedHeaderTree.PeerDisconnected(this.PeerId);
                consensusManager.BlockPuller.PeerDisconnected(this.PeerId);
                consensusManager.ProcessDownloadQueueLocked();
            }
            else
                this.logger.LogTrace("Peer {0} was already removed.", this.PeerId);

            this.logger.LogTrace("(-)");
        }
    }
}
