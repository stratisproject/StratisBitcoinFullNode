using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    /// <summary>
    /// A list of headers are presented from a given peer,
    /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
    /// </summary>
    public sealed class HeadersPresentedVisitor : IConsensusVisitor<ConnectNewHeadersResult>
    {
        private readonly ILogger logger;

        /// <summary>The list of new headers.</summary>
        public List<BlockHeader> Headers { get; private set; }

        /// <summary>The peer that providing the headers.</summary>
        private readonly INetworkPeer peer;

        /// <summary>Specifies if the download should be scheduled for interesting blocks.</summary>
        private readonly bool triggerDownload;

        public HeadersPresentedVisitor(ILoggerFactory loggerFactory, INetworkPeer peer, List<BlockHeader> headers, bool triggerDownload)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.peer = peer;
            this.Headers = headers;
            this.triggerDownload = triggerDownload;
        }

        /// <inheritdoc/>
        public Task<ConnectNewHeadersResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4},{5}:{6})", nameof(this.peer.Connection.Id), this.peer.Connection.Id, nameof(this.Headers), nameof(this.Headers.Count), this.Headers.Count, nameof(this.triggerDownload), this.triggerDownload);

            ConnectNewHeadersResult connectNewHeadersResult;

            lock (consensusManager.PeerLock)
            {
                int peerId = this.peer.Connection.Id;

                connectNewHeadersResult = consensusManager.ChainedHeaderTree.ConnectNewHeaders(peerId, this.Headers);
                consensusManager.BlockPuller.NewPeerTipClaimed(this.peer, connectNewHeadersResult.Consumed);

                if (!consensusManager.PeersByPeerId.ContainsKey(peerId))
                {
                    consensusManager.PeersByPeerId.Add(peerId, this.peer);
                    this.logger.LogTrace("New peer with ID {0} was added.", peerId);
                }
            }

            if (this.triggerDownload && (connectNewHeadersResult.DownloadTo != null))
                consensusManager.BlockDownloader.DownloadBlocks(connectNewHeadersResult.ToArray(), consensusManager.BlockDownloader.ProcessDownloadedBlock);

            this.logger.LogTrace("(-):'{0}'", connectNewHeadersResult);
            return Task.FromResult(connectNewHeadersResult);
        }
    }
}
