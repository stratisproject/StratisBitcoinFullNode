using System;
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
        : base(chain, initialBlockDownloadState, consensusManager, peerBanning, loggerFactory)
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
                    await this.ProcessGetHeadersAsync(peer, getHeaders).ConfigureAwait(false);
                    break;

                case PoAHeadersPayload headers:
                    await this.ProcessHeadersAsync(peer, headers.Headers.Cast<BlockHeader>().ToList()).ConfigureAwait(false);
                    break;
            }
        }

        /// <inheritdoc />
        /// <remarks>Creates <see cref="PoAHeadersPayload"/> instead of <see cref="HeadersPayload"/> like base implementation does.</remarks>
        protected override Payload ConstructHeadersPayload(GetHeadersPayload getHeadersPayload, out ChainedHeader lastHeader)
        {
            ChainedHeader fork = this.chain.FindFork(getHeadersPayload.BlockLocator);
            lastHeader = null;

            if (fork == null)
            {
                this.logger.LogTrace("(-)[INVALID_LOCATOR]:null");
                return null;
            }

            var headersPayload = new PoAHeadersPayload();

            foreach (ChainedHeader chainedHeader in this.chain.EnumerateToTip(fork).Skip(1))
            {
                lastHeader = chainedHeader;

                if (chainedHeader.Header is PoABlockHeader header)
                {
                    headersPayload.Headers.Add(header);

                    if ((chainedHeader.HashBlock == getHeadersPayload.HashStop) || (headersPayload.Headers.Count == MaxItemsPerHeadersMessage))
                        break;
                }
                else
                {
                    throw new Exception("Not a PoA header!");
                }
            }

            this.logger.LogTrace("{0} headers were selected for sending, last one is '{1}'.", headersPayload.Headers.Count, headersPayload.Headers.LastOrDefault()?.GetHash());

            return headersPayload;
        }
    }
}
