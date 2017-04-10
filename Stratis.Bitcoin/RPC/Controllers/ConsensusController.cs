using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.BlockStore;

namespace Stratis.Bitcoin.RPC.Controllers
{
    public class ConsensusController : BaseRPCController
    {
        public ConsensusController(ChainBehavior.ChainState chainState = null, ConsensusLoop consensusLoop = null, ConcurrentChain chain = null)
            : base(chainState: chainState, consensusLoop: consensusLoop, chain: chain) { }

        [ActionName("getbestblockhash")]
        public uint256 GetBestBlockHash()
        {
            Guard.NotNull(this._ChainState, nameof(_ChainState));
            return this._ChainState?.HighestValidatedPoW?.HashBlock;
        }

        [ActionName("getblockhash")]
        public uint256 GetBlockHash(int height)
        {
            Guard.NotNull(this._ConsensusLoop, nameof(_ConsensusLoop));
            Guard.NotNull(this._Chain, nameof(_Chain));

            uint256 bestBlockHash = this._ConsensusLoop.Tip?.HashBlock;
            ChainedBlock bestBlock = bestBlockHash == null ? null : this._Chain.GetBlock(bestBlockHash);
            if (bestBlock == null)
                return null;
            ChainedBlock block = this._Chain.GetBlock(height);
            return block == null || block.Height > bestBlock.Height ? null : block.HashBlock;
        }
    }
}
