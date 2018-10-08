using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.PoA.Payloads;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAConsensusManagerBehavior : ConsensusManagerBehavior
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public PoAConsensusManagerBehavior(ConcurrentChain chain, IInitialBlockDownloadState initialBlockDownloadState,
            IConsensusManager consensusManager, IPeerBanning peerBanning, ILoggerFactory loggerFactory)
        : base (chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        /// <inheritdoc />
        /// <remarks>It replaces processing normal headers payloads with processing PoA headers payload.</remarks>
        protected override async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case GetHeadersPayload getHeaders:
                    // TODO POA override to send PoA headers as a response
                    //await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;

                case PoAHeadersPayload headers:
                    await this.ProcessHeadersAsync(peer, headers.Headers.Select(x => x as BlockHeader).ToList()).ConfigureAwait(false);
                    break;
            }
        }
    }
}
