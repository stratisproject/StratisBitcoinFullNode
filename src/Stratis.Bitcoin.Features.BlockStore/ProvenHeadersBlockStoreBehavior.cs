using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <inheritdoc />
    public class ProvenHeadersBlockStoreBehavior : BlockStoreBehavior
    {
        private readonly Network network;

        /// <summary>
        /// Gets or sets the height from which start serving Proven Headers, if > 0.
        /// </summary>
        public int AnnounceProvenHeadersFromHeight { get; set; }

        public ProvenHeadersBlockStoreBehavior(Network network, ConcurrentChain chain, IChainState chainState, ILoggerFactory loggerFactory, IConsensusManager consensusManager)
            : base(chain, chainState, loggerFactory, consensusManager)
        {
            this.network = Guard.NotNull(network, nameof(network));
        }

        /// <inheritdoc />
        protected override async Task ProcessMessageAsync(INetworkPeer peer, IncomingMessage message)
        {
            switch (message.Message.Payload)
            {
                case SendProvenHeadersPayload sendProvenHeadersPayload:
                    await this.ProcessSendProvenHeadersPayload(sendProvenHeadersPayload);
                    break;

                default:
                    await base.ProcessMessageAsync(peer, message).ConfigureAwait(false); ;
                    break;
            }
        }

        private Task ProcessSendProvenHeadersPayload(SendProvenHeadersPayload sendProvenHeadersPayload)
        {
            this.PreferHeaders = true;

            var provenHeadersActivationHeight = (this.network.Consensus.Options as PosConsensusOptions).ProvenHeadersActivationHeight;
            // Ensures we don't announce ProvenHeaders before ProvenHeadersActivationHeight.
            this.AnnounceProvenHeadersFromHeight = Math.Max(provenHeadersActivationHeight, sendProvenHeadersPayload.RequireFromHeight);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <returns>The <see cref="HeadersPayload"/> instance to announce to the peer, or <see cref="ProvenHeadersPayload"/> if the peers requires it.</returns>
        protected override Payload BuildAnnouncedHeaderPayload(int blockstoreTipHeight, params BlockHeader[] headers)
        {
            if (this.AnnounceProvenHeadersFromHeight > 0 && blockstoreTipHeight >= this.AnnounceProvenHeadersFromHeight)
            {
                ProvenHeadersPayload provenHeadersPayload = new ProvenHeadersPayload();
                foreach (var header in headers)
                {
                    // When announcing proven headers we will always announce headers that we received form peers,
                    // this means the BlockHeader must already be of type ProvenBlockHeader.
                    ProvenBlockHeader provenBlockHeader = header as ProvenBlockHeader;

                    if (provenBlockHeader == null)
                    {
                        throw new BlockStoreException("BlockHeader is expected to be a ProvenBlockHeader");
                    }

                    provenHeadersPayload.Headers.Add(provenBlockHeader);
                }

                return provenHeadersPayload;
            }
            else
            {
                return base.BuildAnnouncedHeaderPayload(blockstoreTipHeight, headers);
            }
        }

        public override object Clone()
        {
            var res = new ProvenHeadersBlockStoreBehavior(this.network, this.chain, this.chainState, this.loggerFactory, this.consensusManager)
            {
                CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
                CanRespondToGetDataPayload = this.CanRespondToGetDataPayload
            };

            return res;
        }

    }
}
