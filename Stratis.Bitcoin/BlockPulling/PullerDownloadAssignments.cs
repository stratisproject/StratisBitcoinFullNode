using Microsoft.Extensions.Logging;
using NBitcoin;
using NLog.Extensions.Logging;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Implements a strategy for a block puller that needs to download 
    /// a set of blocks from its connected peers.
    /// </summary>
    public static class PullerDownloadAssignments
    {
        /// <summary>Information about a connected peer.</summary>
        public class PeerInformation
        {
            /// <summary>Evaluation of the node's past experience with the peer.</summary>
            public double QualityScore { get; set; }

            /// <summary>Length of the chain the peer maintains.</summary>
            public int ChainHeight { get; set; }

            /// <summary>Application defined peer identifier.</summary>
            public object PeerId { get; set; }

            /// <summary>Number of tasks assigned to this peer.</summary>
            public int TasksAssignedCount { get; set; }
        }

        /// <summary>Class logger.</summary>
        private static readonly ILogger logger;

        /// <summary>Number of blocks from the currently last block that are protected from being assigned to poor peers.</summary>
        /// <seealso cref="AssignBlocksToPeers"/>
        private const int CriticalLookahead = 10;

        /// <summary>Peer is considered to be assigned a large amount of work if it has more than this amount of download tasks assigned.</summary>
        private const int HighWorkAmountThreshold = 50;

        /// <summary>Random number generator.</summary>
        private static Random Rand = new Random();

        /// <summary>
        /// Initializes class logger.
        /// </summary>
        static PullerDownloadAssignments()
        {
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddNLog();
            logger = loggerFactory.CreateLogger(typeof(PullerDownloadAssignments).FullName);
        }

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
        /// <remarks>
        /// Peers with a lot of work (more than <see cref="HighWorkAmountThreshold"/>) already assigned to them have less chance 
        /// getting more work. However, the quality is stronger factor.
        /// <para>
        /// Tasks to download blocks with height in the lower half of the requested block heights 
        /// are protected from being assigned to peers with quality below the median quality of available peers.
        /// </para>
        /// </remarks>
        public static Dictionary<PeerInformation, List<int>> AssignBlocksToPeers(List<int> requestedBlockHeights, List<PeerInformation> availablePeersInformation)
        {
            logger.LogTrace($"({nameof(requestedBlockHeights)}:{string.Join(",", requestedBlockHeights)},{nameof(availablePeersInformation)}.{nameof(availablePeersInformation.Count)}:{availablePeersInformation.Count})");

            Dictionary<PeerInformation, List<int>> res = new Dictionary<PeerInformation, List<int>>();
            foreach (PeerInformation peerInformation in availablePeersInformation)
                res.Add(peerInformation, new List<int>());

            int medianBlockHeight = requestedBlockHeights.Median();

            foreach (int blockHeight in requestedBlockHeights)
            {
                // Only consider peers that have the chain long enough to be able to provide block at blockHeight height.
                List<PeerInformation> filteredPeers = availablePeersInformation.Where(p => p.ChainHeight >= blockHeight).ToList();

                if (filteredPeers.Count == 0)
                    continue;

                double[] scores = filteredPeers.Select(n => n.QualityScore).ToArray();
                if (blockHeight < medianBlockHeight)
                {
                    // This block task is protected, filter out peers with low quality.
                    double median = scores.Median();
                    filteredPeers = filteredPeers.Where(n => n.QualityScore >= median).ToList();

                    if (filteredPeers.Count == 0)
                        continue;

                    scores = filteredPeers.Select(n => n.QualityScore).ToArray();
                }

                // Modify scores based on amount of work peers already have.
                // The peer may get up to 50 % penalty to its score if it has too much work on itself.
                int totalWork = filteredPeers.Sum(p => p.TasksAssignedCount);
                for (int i = 0; i < filteredPeers.Count; i++)
                {
                    int peerTaskCount = filteredPeers[i].TasksAssignedCount;
                    if (peerTaskCount > HighWorkAmountThreshold)
                    {
                        double penaltyCoef = 1 + ((peerTaskCount * peerTaskCount) / (totalWork * totalWork));
                        scores[i] /= penaltyCoef;
                    }
                }

                // Randomly choose a peer to assign the task to with respect to scores of all filtered peers.
                int[] intScores = scores.Select(s => (int)(s * 100.0)).ToArray();
                int totalScore = intScores.Sum();

                int index = GetNodeIndex(intScores, totalScore);
                PeerInformation selectedPeer = filteredPeers[index];

                // Assign the task to download block with height blockHeight to the selected peer.
                res[selectedPeer].Add(blockHeight);
            }

            logger.LogTrace($"(-):*.{nameof(res.Count)}={res.Count}");
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
            int selectedScore = Rand.Next(totalScore);
            int current = 0;
            int i = 0;
            foreach (int score in scores)
            {
                current += score;
                if (selectedScore < current)
                    return i;
                i++;
            }
            return scores.Length - 1;
        }
    }
}
