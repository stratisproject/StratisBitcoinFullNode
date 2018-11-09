using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// Allows readonly access to various private or internal <see cref="ConsensusManager"/> fields.
    /// </summary>
    public sealed class ConsensusManagerState
    {
        /// <summary> <see cref="ConsensusManager.callbacksByBlocksRequestedHash"/></summary>
        public Dictionary<uint256, List<OnBlockDownloadedCallback>> CallbacksByBlocksRequestedHash { get; internal set; }

        /// <summary> <see cref="ConsensusManager.expectedBlockDataBytes"/></summary>
        public long ExpectedBlockDataBytes { get; internal set; }

        /// <summary> <see cref="ConsensusManager.expectedBlockSizes"/></summary>
        public Dictionary<uint256, long> ExpectedBlockSizes { get; internal set; }

        /// <summary> <see cref="ConsensusManager.peersByPeerId"/></summary>
        public Dictionary<int, INetworkPeer> PeersByPeerId { get; internal set; }

        /// <summary> Allows access to <see cref="ChainedHeaderTree"/> private fields.</summary>
        public ChainedHeaderTreeState ChainedHeaderTreeState { get; internal set; }
    }
}
