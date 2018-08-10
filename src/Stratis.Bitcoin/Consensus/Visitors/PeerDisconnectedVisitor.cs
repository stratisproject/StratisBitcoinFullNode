using System.Threading.Tasks;
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

        public Task<ConsensusVisitorResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1})", nameof(this.PeerId), this.PeerId);

            lock (consensusManager.PeerLock)
            {
                consensusManager.PeerDisconnectedLocked(this.PeerId);
            }

            this.logger.LogTrace("(-)");

            return Task.FromResult(new ConsensusVisitorResult());
        }
    }
}
