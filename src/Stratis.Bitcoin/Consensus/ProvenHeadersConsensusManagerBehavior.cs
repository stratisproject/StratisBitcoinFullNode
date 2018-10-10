using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>Behavior that takes care of proven headers protocol. It also keeps the notion of peer's consensus tip.</summary>
    public class ProvenHeadersConsensusManagerBehavior : ConsensusManagerBehavior
    {
        private readonly IInitialBlockDownloadState initialBlockDownloadState;
        private readonly IConsensusManager consensusManager;
        private readonly IPeerBanning peerBanning;
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public ProvenHeadersConsensusManagerBehavior(ConcurrentChain chain, IInitialBlockDownloadState initialBlockDownloadState, IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory) : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
        {
            this.chain = chain;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.consensusManager = consensusManager;
            this.peerBanning = peerBanning;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <inheritdoc />
        /// <summary>
        /// Processes and incoming message from the peer.
        /// </summary>
        /// <param name="peer">Peer from which the message was received.</param>
        /// <param name="message">Received message to process.</param>
        protected override async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            await base.OnMessageReceivedAsync(peer, message);
            switch (message.Message.Payload)
            {
                case ProvenHeadersPayload provenHeaders:
                    await this.ProcessHeadersAsync(peer, provenHeaders.Headers.Cast<BlockHeader>().ToList()).ConfigureAwait(false);
                    break;

                case GetProvenHeadersPayload getHeaders:
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;
            }
        }

        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync, true);
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new ProvenHeadersConsensusManagerBehavior(this.chain, this.initialBlockDownloadState, this.consensusManager, this.peerBanning, this.loggerFactory);
        }
    }
}
