using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.RPC.Controllers
{
    public class ConsensusController : BaseRPCController
    {
        public ConsensusController(ConsensusLoop consensusLoop, ConcurrentChain chain) : base(consensusLoop: consensusLoop, chain: chain)
        {
            Guard.NotNull(this._ConsensusLoop, nameof(_ConsensusLoop));
            Guard.NotNull(this._Chain, nameof(_Chain));
        }

        [ActionName("getbestblockhash")]
        public uint256 GetBestBlockHash()
        {
            return this._ConsensusLoop.Tip.HashBlock;
        }

        [ActionName("getblockhash")]
        public uint256 GetBlockHash(int height)
        {
            uint256 bestBlockHash = this._ConsensusLoop.Tip.HashBlock;
            ChainedBlock bestBlock = this._Chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
                return null;
            ChainedBlock block = this._Chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }
    }
}
