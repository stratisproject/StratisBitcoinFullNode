﻿using System;
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
using TracerAttributes;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <inheritdoc />
    public class ProvenHeadersBlockStoreBehavior : BlockStoreBehavior
    {
        private readonly Network network;
        private readonly ICheckpoints checkpoints;

        public ProvenHeadersBlockStoreBehavior(Network network, ConcurrentChain chain, IChainState chainState, ILoggerFactory loggerFactory, IConsensusManager consensusManager, ICheckpoints checkpoints)
            : base(chain, chainState, loggerFactory, consensusManager)
        {
            this.network = Guard.NotNull(network, nameof(network));
            this.checkpoints = Guard.NotNull(checkpoints, nameof(checkpoints));
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
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <returns>The <see cref="HeadersPayload"/> instance to announce to the peer, or <see cref="ProvenHeadersPayload"/> if the peers requires it.</returns>
        protected override Payload BuildAnnouncedHeaderPayload(int blockstoreTipHeight, params BlockHeader[] headers)
        {
            var provenHeadersPayload = new ProvenHeadersPayload();

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

        [NoTrace]
        public override object Clone()
        {
            var res = new ProvenHeadersBlockStoreBehavior(this.network, this.chain, this.chainState, this.loggerFactory, this.consensusManager, this.checkpoints)
            {
                CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
                CanRespondToGetDataPayload = this.CanRespondToGetDataPayload
            };

            return res;
        }

    }
}
