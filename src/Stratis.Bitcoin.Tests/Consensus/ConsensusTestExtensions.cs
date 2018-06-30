using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.Tests.Consensus
{
    internal static class ConsensusTestExtensions
    {
        public static Dictionary<uint256, HashSet<int>> GetPeerIdsByTipHash(this ChainedHeaderTree chainedHeaderTree)
        {
            return chainedHeaderTree.GetMemberValue("peerIdsByTipHash") as Dictionary<uint256, HashSet<int>>;
        }

        public static Dictionary<int, uint256> GetPeerTipsByPeerId(this ChainedHeaderTree chainedHeaderTree)
        {
            return chainedHeaderTree.GetMemberValue("peerTipsByPeerId") as Dictionary<int, uint256>;
        }

        public static Dictionary<uint256, ChainedHeader> GetChainedHeadersByHash(this ChainedHeaderTree chainedHeaderTree)
        {
            return chainedHeaderTree.GetMemberValue("chainedHeadersByHash") as Dictionary<uint256, ChainedHeader>;
        }

        public static ChainedHeader GetPeerTipChainedHeaderByPeerId(this ChainedHeaderTree chainedHeaderTree, int peer)
        {
            return chainedHeaderTree.GetChainedHeadersByHash()[chainedHeaderTree.GetPeerTipsByPeerId()[peer]];
        }

        public static bool HaveBlockDataAvailabilityStateOf(this ConnectNewHeadersResult connectNewHeadersResult, BlockDataAvailabilityState blockDataAvailabilityState)
        {
            if (connectNewHeadersResult.DownloadFrom == null || connectNewHeadersResult.DownloadTo == null) return false;
            ChainedHeader chainedHeader = connectNewHeadersResult.DownloadTo;
            while (chainedHeader.Height > connectNewHeadersResult.DownloadFrom.Height)
            {
                if (chainedHeader.BlockDataAvailability != blockDataAvailabilityState) return false;
                chainedHeader = chainedHeader.Previous;
            }
            return true;
        }
    }
}