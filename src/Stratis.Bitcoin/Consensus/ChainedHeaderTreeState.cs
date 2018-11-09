using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Allows readonly access to various private or internal <see cref="ChainedHeaderTree"/> fields.
    /// </summary>
    public sealed class ChainedHeaderTreeState
    {
        /// <summary> <see cref="ChainedHeaderTree.chainedHeadersByHash"/></summary>
        public Dictionary<uint256, ChainedHeader> ChainedHeadersByHash { get; internal set; }

        /// <summary> <see cref="ChainedHeaderTree.peerTipsByPeerId"/></summary>
        public Dictionary<int, uint256> PeerTipsByPeerId { get; internal set; }

        /// <summary> <see cref="ChainedHeaderTree.peerIdsByTipHash"/></summary>
        public Dictionary<uint256, HashSet<int>> PeerIdsByTipHash { get; internal set; }
    }
}
