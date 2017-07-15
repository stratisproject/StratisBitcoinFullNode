using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Collections.Concurrent;
using NBitcoin.Protocol.Behaviors;
using System.Threading;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Implements a strategy for a block puller that needs to download 
    /// a set of blocks and from its connected peers.
    /// </summary>
    public static class DownloadAssignmentStrategy
    {
        /// <summary>Information about a connected peer.</summary>
        public class PeerInformation
        {
            /// <summary>Evaluation of the node's past experience with the peer.</summary>
            public int QualityScore;

            /// <summary>Length of the chain the peer maintains.</summary>
            public int ChainHeight;

            /// <summary>Application defined peer identifier.</summary>
            public object PeerId;
        }

        /// <summary>Random number generator.</summary>
        private static Random Rand = new Random();

        /// <summary>
        /// Having a list of block heights of the blocks that needs to be downloaded and having a list of available 
        /// peer nodes that can be asked to provide the blocks, this method selects which peer is asked to provide
        /// which block.
        /// <para>
        /// Past experience with the node is considered when assigning the task to a peer.
        /// Peers with better quality score tend to get more work than others.
        /// </para>
        /// </summary>
        /// <param name="requestedBlockHeights">List of block heights that needs to be downloaded.</param>
        /// <param name="availablePeersInformation">List of peers that are available including information about lengths of their chains.</param>
        /// <returns>List of block heights that each peer is assigned, mapped by information about peers.</returns>
        public static Dictionary<PeerInformation, List<int>> AssignBlocksToPeers(List<int> requestedBlockHeights, List<PeerInformation> availablePeersInformation)
        {
            Dictionary<PeerInformation, List<int>> res = new Dictionary<PeerInformation, List<int>>();
            foreach (PeerInformation peerInformation in availablePeersInformation)
                res.Add(peerInformation, new List<int>());

            foreach (int blockHeight in requestedBlockHeights)
            {
                // Only consider peers that have the chain long enough to be able to provide block at blockHeight height.
                List<PeerInformation> filteredPeers = availablePeersInformation.Where(p => p.ChainHeight >= blockHeight).ToList();

                int[] scores = filteredPeers.Select(n => n.QualityScore == BlockPuller.MaxQualityScore ? BlockPuller.MaxQualityScore * 2 : n.QualityScore).ToArray();
                int totalScore = scores.Sum();

                // Randomly choose a peer to assign the task to with respect to scores of all filtered peers.
                int index = GetNodeIndex(scores, totalScore);
                PeerInformation selectedPeer = filteredPeers[index];

                // Assign the task to download block with height blockHeight to the selected peer.
                res[selectedPeer].Add(blockHeight);
            }

            return res;
        }

        /// <summary>
        /// Choose random index proportional to the score.
        /// </summary>
        /// <param name="scores">Array of scores.</param>
        /// <param name="totalScore">Sum of the values in <paramref name="scores"/>.</param>
        /// <returns>Random index to <paramref name="scores"/> array - i.e. a number from 0 to scores.Length - 1.</returns>
        private static int GetNodeIndex(int[] scores, int totalScore)
        {
            int pickScore = Rand.Next(totalScore);
            int current = 0;
            int i = 0;
            foreach (int score in scores)
            {
                current += score;
                if (pickScore < current)
                    return i;
                i++;
            }
            return scores.Length - 1;
        }
    }
}
