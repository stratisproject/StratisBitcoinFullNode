using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
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
        /// <returns>The <see cref="HeadersPayload"/> instance to announce to the peer, or <see cref="ProvenHeadersPayload"/> if the peers requires it.</returns>
        protected override Payload BuildHeadersAnnouncePayload(IEnumerable<BlockHeader> headers)
        {
            var provenHeadersPayload = new ProvenHeadersPayload();

            foreach (var header in headers)
            {
                // When announcing proven headers we will always announce headers that we received form peers,
                // this means the BlockHeader must already be of type ProvenBlockHeader.
                var provenBlockHeader = header as ProvenBlockHeader;

                if (provenBlockHeader == null)
                {
                    // Sanity check. That should never happen.
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
