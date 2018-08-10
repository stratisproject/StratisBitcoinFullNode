using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Consensus.Visitors
{
    public sealed class HeadersPresentedVisitor : IConsensusVisitor<ConnectNewHeadersResult>
    {
        private readonly ILogger logger;
        private readonly List<BlockHeader> headers;
        private readonly INetworkPeer peer;
        private readonly bool triggerDownload;

        /// <summary>
        /// A list of headers are presented from a given peer,
        /// we'll attempt to connect the headers to the tree and if new headers are found they will be queued for download.
        /// </summary>
        /// <param name="peer">The peer that providing the headers.</param>
        /// <param name="headers">The list of new headers.</param>
        /// <param name="triggerDownload">Specifies if the download should be scheduled for interesting blocks.</param>
        /// <returns>Information about consumed headers.</returns>
        /// <exception cref="ConnectHeaderException">Thrown when first presented header can't be connected to any known chain in the tree.</exception>
        /// <exception cref="CheckpointMismatchException">Thrown if checkpointed header doesn't match the checkpoint hash.</exception>
        public HeadersPresentedVisitor(ILoggerFactory loggerFactory, INetworkPeer peer, List<BlockHeader> headers, bool triggerDownload)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType());
            this.peer = peer;
            this.headers = headers;
            this.triggerDownload = triggerDownload;
        }

        public Task<ConnectNewHeadersResult> VisitAsync(ConsensusManager consensusManager)
        {
            this.logger.LogTrace("({0}:{1},{2}.{3}:{4},{5}:{6})", nameof(this.peer.Connection.Id), this.peer.Connection.Id, nameof(this.headers), nameof(this.headers.Count), this.headers.Count, nameof(this.triggerDownload), this.triggerDownload);

            ConnectNewHeadersResult connectNewHeadersResult;

            lock (consensusManager.PeerLock)
            {
                int peerId = this.peer.Connection.Id;

                connectNewHeadersResult = consensusManager.ChainedHeaderTree.ConnectNewHeaders(peerId, this.headers);
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
