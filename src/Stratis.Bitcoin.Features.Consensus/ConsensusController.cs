using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class ConsensusController : FeatureController
    {
        private readonly ILogger logger;

        public IConsensusLoop ConsensusLoop { get; private set; }

        public ConsensusController(ILoggerFactory loggerFactory, IChainState chainState = null,
            IConsensusLoop consensusLoop = null, ConcurrentChain chain = null)
            : base(chainState: chainState, chain: chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.ConsensusLoop = consensusLoop;
        }

        [ActionName("getbestblockhash")]
        [ActionDescription("Get the hash of the block at the consensus tip.")]
        public uint256 GetBestBlockHash()
        {
            Guard.NotNull(this.ChainState, nameof(this.ChainState));
            return this.ChainState?.ConsensusTip?.HashBlock;
        }

        [ActionName("getblockhash")]
        [ActionDescription("Gets the hash of the block at the given height.")]
        public uint256 GetBlockHash(int height)
        {
            Guard.NotNull(this.ConsensusLoop, nameof(this.ConsensusLoop));
            Guard.NotNull(this.Chain, nameof(this.Chain));

            this.logger.LogDebug("RPC GetBlockHash {0}", height);

            uint256 bestBlockHash = this.ConsensusLoop.Tip?.HashBlock;
            ChainedHeader bestBlock = bestBlockHash == null ? null : this.Chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
                return null;
            ChainedHeader block = this.Chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }
    }
}
