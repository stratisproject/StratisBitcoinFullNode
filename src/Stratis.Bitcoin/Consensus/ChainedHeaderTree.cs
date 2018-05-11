using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.Consensus
{
    /// <summary>
    /// A tree structure of <see cref="ChainedHeader "/> elements.
    /// </summary>
    /// <remarks>
    /// A blockchain can have multiple versions of the chain, with only one being the longest chain (or the chain with most proof).
    /// While this is not ideal it can happen naturally or maliciously, to be able to find the best chain we have to keep track of all chains we discover.
    /// Only one chain will represent the tip of the blockchain. 
    /// </remarks>
    public sealed class ChainedHeaderTree
    {
        private readonly Dictionary<uint256, INetworkPeer> peerTipsByHash;

        private readonly Dictionary<uint256, ChainedHeader> chainedHeadersByHash;

        private readonly object lockObject = new object();

        public void HeadersPresented(List<BlockHeader> headers)
        {
            if (!this.chainedHeadersByHash.ContainsKey(headers.First().HashPrevBlock))
            {
                throw new Exception("Can't connect");
            }
        }


        public void PeerDiconnect(NetworkPeer networkPeer)
        {
            lock (this.lockObject)
            {
                this.peerTipsByHash.Remove(tip.HashBlock, networkPeer);
            }

        }

        private ChainedHeader CreateChainedBlock(List<BlockHeader> headers)
        {

        }
    }
}
