using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
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

        public ProvenHeadersBlockStoreBehavior(Network network, ConcurrentChain chain, IChainState chainState, ILoggerFactory loggerFactory, IConsensusManager consensusManager, ICheckpoints checkpoints, IBlockStoreQueue blockStoreQueue)
            : base(chain, chainState, loggerFactory, consensusManager, blockStoreQueue)
        {
            this.network = Guard.NotNull(network, nameof(network));
            this.checkpoints = Guard.NotNull(checkpoints, nameof(checkpoints));
        }

        /// <inheritdoc />
        /// <returns>The <see cref="HeadersPayload"/> instance to announce to the peer, or <see cref="ProvenHeadersPayload"/> if the peers requires it.</returns>
        protected override Payload BuildHeadersAnnouncePayload(IEnumerable<BlockHeader> headers)
        {
            // Sanity check. That should never happen.
            if (!headers.All(x => x is ProvenBlockHeader))
                throw new BlockStoreException("UnexpectedError: BlockHeader is expected to be a ProvenBlockHeader");

            var provenHeadersPayload = new ProvenHeadersPayload(headers.Cast<ProvenBlockHeader>().ToArray());

            return provenHeadersPayload;
        }

        [NoTrace]
        public override object Clone()
        {
            var res = new ProvenHeadersBlockStoreBehavior(this.network, this.chain, this.chainState, this.loggerFactory, this.consensusManager, this.checkpoints, this.blockStoreQueue)
            {
                CanRespondToGetBlocksPayload = this.CanRespondToGetBlocksPayload,
                CanRespondToGetDataPayload = this.CanRespondToGetDataPayload
            };

            return res;
        }
    }
}
