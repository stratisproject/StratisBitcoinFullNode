using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Consensus
{
    public sealed class ConsensusManagerState
    {
        public Dictionary<uint256, List<OnBlockDownloadedCallback>> CallbacksByBlocksRequestedHash { get; internal set; }

        public long ExpectedBlockDataBytes { get; internal set; }

        public Dictionary<uint256, long> ExpectedBlockSizes { get; internal set; }

        public Dictionary<int, INetworkPeer> PeersByPeerId { get; internal set; }

        public ChainedHeaderTreeState ChainedHeaderTreeState { get; internal set; }
    }
}
