using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.BlockStore;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;

namespace Stratis.Bitcoin.RPC.Controllers
{
    public class ConsensusController : BaseRPCController
    {
        private readonly ILogger logger;

        public ConsensusController(ILoggerFactory loggerFactory, ChainBehavior.ChainState chainState = null,
            ConsensusLoop consensusLoop = null, ConcurrentChain chain = null)
            : base(chainState: chainState, consensusLoop: consensusLoop, chain: chain)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        [ActionName("getbestblockhash")]
        public uint256 GetBestBlockHash()
        {
            Guard.NotNull(this.ChainState, nameof(this.ChainState));
            return this.ChainState?.HighestValidatedPoW?.HashBlock;
        }

        [ActionName("getblockhash")]
        public uint256 GetBlockHash(int height)
        {
            Guard.NotNull(this.ConsensusLoop, nameof(this.ConsensusLoop));
            Guard.NotNull(this.Chain, nameof(this.Chain));

            this.logger.LogDebug("RPC GetBlockHash {0}", height);

            uint256 bestBlockHash = this.ConsensusLoop.Tip?.HashBlock;
            ChainedBlock bestBlock = bestBlockHash == null ? null : this.Chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
                return null;
            ChainedBlock block = this.Chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }
    }
}
