using System.Collections.Generic;
using System.Linq;
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

        public static ChainedHeader[] ToArray(this ChainedHeader chainedHeader, int headersToTake)
        {
            var headers = new ChainedHeader[headersToTake];
            ChainedHeader current = chainedHeader;

            for (int i = headersToTake - 1; i >= 0 && current != null; i--)
            {
                headers[i] = current;
                current = current.Previous;
            }

            return headers;
        }
    }
}