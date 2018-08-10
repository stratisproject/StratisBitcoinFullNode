using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    ///// <summary>
    ///// Called after a peer was disconnected.
    ///// Informs underlying components about the even.
    ///// Processes any remaining blocks to download.
    ///// </summary>
    ///// <param name="peerId">The peer that was disconnected.</param>
    public sealed class PeerDisconnectedVisitor : IConsensusVisitor<PeerDisconnectedVisitorResult>
    {
        private readonly ILogger logger;
        private readonly int peerId;

        public PeerDisconnectedVisitor(ILoggerFactory loggerFactory, int peerId)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.peerId = peerId;
        }

        public Task<PeerDisconnectedVisitorResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1})", nameof(this.peerId), this.peerId);

            lock (consensusManager.PeerLock)
            {
                consensusManager.PeerDisconnectedLocked(this.peerId);
            }

            this.logger.LogTrace("(-)");

            return Task.FromResult(new PeerDisconnectedVisitorResult());
        }
    }

    public sealed class PeerDisconnectedVisitorResult { }
}
