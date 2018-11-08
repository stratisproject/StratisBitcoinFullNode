using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
    public sealed class ChainedHeaderTreeState
    {
        public Dictionary<uint256, ChainedHeader> ChainedHeadersByHash { get; internal set; }

        public Dictionary<int, uint256> PeerTipsByPeerId { get; internal set; }

        public Dictionary<uint256, HashSet<int>> PeerIdsByTipHash { get; internal set; }
    }
}
