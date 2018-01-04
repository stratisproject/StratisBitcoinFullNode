using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SCChain
    {
        public List<SCBlock> Chain { get; set; }
        public Dictionary<uint256,SCBlock> BlocksById { get; set; }
        public Network Network { get; set; }

        public SCChain(Network network)
        {
            Network = network;

            Chain = new List<SCBlock>();
            BlocksById = new Dictionary<uint256, SCBlock>();
        }

        public void SetTip(SCBlock block)
        {
            // Add to chain. TODO: Consider reorgs etc. in future.

            Chain.Add(block);
            BlocksById.Add(block.BlockHash, block);
        }
    }
}
