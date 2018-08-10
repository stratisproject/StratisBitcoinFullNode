using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public sealed class HeadersPresentedVisitor : IConsensusVisitor
    {
        private readonly ILogger logger;

        public List<BlockHeader> Headers { get; set; }

        public INetworkPeer Peer { get; set; }

        public bool TriggerDownload { get; set; }

        public HeadersPresentedVisitor(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.TriggerDownload = true;
        }

        public Task<ConsensusVisitorResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4},{5}:{6})", nameof(this.Peer.Connection.Id), this.Peer.Connection.Id, nameof(this.Headers), nameof(this.Headers.Count), this.Headers.Count, nameof(this.TriggerDownload), this.TriggerDownload);

            ConnectNewHeadersResult connectNewHeadersResult;

            lock (consensusManager.PeerLock)
            {
                int peerId = this.Peer.Connection.Id;

                connectNewHeadersResult = consensusManager.ChainedHeaderTree.ConnectNewHeaders(peerId, this.Headers);
                consensusManager.BlockPuller.NewPeerTipClaimed(this.Peer, connectNewHeadersResult.Consumed);

                if (!consensusManager.PeersByPeerId.ContainsKey(peerId))
                {
                    consensusManager.PeersByPeerId.Add(peerId, this.Peer);
                    this.logger.LogTrace("New peer with ID {0} was added.", peerId);
                }
            }

            if (this.TriggerDownload && (connectNewHeadersResult.DownloadTo != null))
                consensusManager.BlockDownloader.DownloadBlocks(connectNewHeadersResult.ToArray(), consensusManager.BlockDownloader.ProcessDownloadedBlock);

            this.logger.LogTrace("(-):'{0}'", connectNewHeadersResult);
            return Task.FromResult(new ConsensusVisitorResult());
        }
    }
}
