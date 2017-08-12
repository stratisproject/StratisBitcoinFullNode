using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling
{
    /// <summary>
    /// Tests of <see cref="PullerDownloadAssignments"/> class.
    /// </summary>
    public class PullerDownloadAssignmentsTest
    {
        /// <summary>
        /// Previous implementation of block puller's strategy could lead to a situation in which the node's 
        /// peers were asked for blocks then did not have. This is undesirable.
        /// <para>
        /// We simulate the following scenario in this test:
        /// <list type="bullet">
        /// <item>Our node has a chain with 5 blocks and is connected to 4 peer nodes - A, B, C, D.</item>
        /// <item>Node A has a chain with 4 blocks.</item>
        /// <item>Node B has a chain with 20 blocks.</item>
        /// <item>Node C has a chain with 30 blocks.</item>
        /// <item>Node D has a chain with 40 blocks.</item>
        /// </list>
        /// </para>
        /// <para>
        /// We call AskBlocks on the block puller with requests to download blocks 6 to 40
        /// and we check that the node A is not assigned any work and that the node B is not 
        /// assigned any work for blocks 21 to 40, and C is not assigned any work for blocks 
        /// 31 to 40.
        /// </para>
        /// </summary>
        [Fact]
        public void AssignBlocksToPeersWithNodesWithDifferentChainsCorrectlyDistributesDownloadTasks()
        {
            int ourBlockCount = 5;

            // Create list of numbers ourBlockCount + 1 to 40 and shuffle it.
            Random rnd = new Random();
            List<int> requiredBlockHeights = new List<int>();
            for (int i = ourBlockCount + 1; i <= 40; i++)
                requiredBlockHeights.Add(i);

            requiredBlockHeights = requiredBlockHeights.OrderBy(a => rnd.Next()).ToList();

            // Initialize node's peers.
            List<PullerDownloadAssignments.PeerInformation> availablePeersInformation = new List<PullerDownloadAssignments.PeerInformation>()
            {
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "A",
                    QualityScore = 100,
                    ChainHeight = 4,
                    TasksAssignedCount = 0
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "B",
                    QualityScore = 100,
                    ChainHeight = 20,
                    TasksAssignedCount = 0
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "C",
                    QualityScore = 50,
                    ChainHeight = 30,
                    TasksAssignedCount = 0
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "D",
                    QualityScore = 150,
                    ChainHeight = 40,
                    TasksAssignedCount = 0
                },
            };

            // Use the assignment strategy to assign tasks to peers.
            Dictionary<PullerDownloadAssignments.PeerInformation, List<int>> assignments = PullerDownloadAssignments.AssignBlocksToPeers(requiredBlockHeights, availablePeersInformation);

            // Check the assignment is valid per our requirements.
            int tasksAssigned = 0;
            Assert.Equal(availablePeersInformation.Count, assignments.Count);
            foreach (KeyValuePair<PullerDownloadAssignments.PeerInformation, List<int>> kvp in assignments)
            {
                PullerDownloadAssignments.PeerInformation peer = kvp.Key;
                List<int> assignedBlockHeights = kvp.Value;
                tasksAssigned += assignedBlockHeights.Count;

                switch ((string)peer.PeerId)
                {
                    case "A":
                        // Peer A should not get any work.
                        Assert.Equal(0, assignedBlockHeights.Count);
                        break;

                    case "B":
                    case "C":
                    case "D":
                        // Peers B and C should only get tasks to download blocks up to its chain height.
                        // Peer D can be assigned anything.
                        Assert.True((assignedBlockHeights.Count == 0) || (assignedBlockHeights.Max() <= peer.ChainHeight));
                        break;

                    default:
                        // This should never occur.
                        Assert.True(false, "Invalid peer ID.");
                        break;
                }
            }
            Assert.Equal(requiredBlockHeights.Count, tasksAssigned);
        }

        /// <summary>
        /// Very similar test to AssignBlocksToPeersWithNodesWithDifferentChainsCorrectlyDistributesDownloadTasks
        /// except that this one uses randomized initialization and much larger sample.
        /// </summary>
        [Fact]
        public void AssignBlocksToPeersLargeSampleCorrectlyDistributesDownloadTasks()
        {
            Random rnd = new Random();

            int iterationCount = 1000;
            for (int iteration = 0; iteration < iterationCount; iteration++)
            {
                // Choose scenario for this iteration.
                int ourBlockCount = rnd.Next(1000);
                int bestChainBlockCount = ourBlockCount + rnd.Next(1000);
                int availablePeerCount = rnd.Next(100) + 1;

                List<int> requiredBlockHeights = new List<int>();
                for (int i = ourBlockCount + 1; i <= bestChainBlockCount; i++)
                    requiredBlockHeights.Add(i);

                requiredBlockHeights = requiredBlockHeights.OrderBy(a => rnd.Next()).ToList();

                // Initialize node's peers.
                int maxPeerChainLength = 0;
                List<PullerDownloadAssignments.PeerInformation> availablePeersInformation = new List<PullerDownloadAssignments.PeerInformation>();
                for (int peerIndex = 0; peerIndex < availablePeerCount; peerIndex++)
                {
                    PullerDownloadAssignments.PeerInformation peerInfo = new PullerDownloadAssignments.PeerInformation()
                    {
                        ChainHeight = rnd.Next(bestChainBlockCount) + 1,
                        PeerId = peerIndex,
                        QualityScore = rnd.NextDouble() * (QualityScore.MaxScore - QualityScore.MinScore) + QualityScore.MinScore,
                        TasksAssignedCount = rnd.Next(100)
                    };
                    availablePeersInformation.Add(peerInfo);
                    maxPeerChainLength = Math.Max(maxPeerChainLength, peerInfo.ChainHeight);
                }

                // Use the assignment strategy to assign tasks to peers.
                Dictionary<PullerDownloadAssignments.PeerInformation, List<int>> assignments = PullerDownloadAssignments.AssignBlocksToPeers(requiredBlockHeights, availablePeersInformation);

                // Check the assignment is valid per our requirements.
                int tasksAssigned = 0;
                Assert.Equal(availablePeersInformation.Count, assignments.Count);
                foreach (KeyValuePair<PullerDownloadAssignments.PeerInformation, List<int>> kvp in assignments)
                {
                    PullerDownloadAssignments.PeerInformation peer = kvp.Key;
                    List<int> assignedBlockHeights = kvp.Value;
                    tasksAssigned += assignedBlockHeights.Count;

                    // Peers with shorter chain should not get any work
                    // other peers should not get any work exceeding their knowledge.
                    if (peer.ChainHeight <= ourBlockCount) Assert.Equal(0, assignedBlockHeights.Count);
                    else if (assignedBlockHeights.Count > 0) Assert.True(assignedBlockHeights.Max() <= peer.ChainHeight);
                }

                // Check that all tasks that could be assigned were assigned.
                int taskShouldAssign = requiredBlockHeights.Where(r => r <= maxPeerChainLength).Count();
                Assert.Equal(taskShouldAssign, tasksAssigned);
            }
        }

        /// <summary>
        /// Assignment of tasks to nodes should prevent the very next blocks that the consumer will need 
        /// in the nearest future to be assigned to nodes that are considered of poor quality.
        /// This test checks that this protection works as intended.
        /// <para>
        /// We will request assignment of blocks A to B, and check that the lower half of these requests 
        /// (i.e. blocks from A to median(A..B) are not assigned to nodes having quality score worse than median.
        /// </para>
        /// </summary>
        [Fact]
        public void AssignBlocksToPeersWithNodesWithLowQualityProtectsLowerHalfRequestsFromBeingAssignedToPoorQualityNodes()
        {
            Random rnd = new Random();
            int ourBlockCount = rnd.Next(1000) + 1;
            int bestBlockCount = rnd.Next(1000) + ourBlockCount + 1;

            // Create list of numbers ourBlockCount + 1 to bestBlockCount and shuffle it.
            List<int> requiredBlockHeights = new List<int>();
            for (int i = ourBlockCount + 1; i <= bestBlockCount; i++)
                requiredBlockHeights.Add(i);

            requiredBlockHeights = requiredBlockHeights.OrderBy(a => rnd.Next()).ToList();
            int medianBlockHeight = requiredBlockHeights.Median();

            // Initialize node's peers. We have 5 peers with median quality 100.
            // Thus peers A and E should not receive any work from the lower half of the requests.
            List<PullerDownloadAssignments.PeerInformation> availablePeersInformation = new List<PullerDownloadAssignments.PeerInformation>()
            {
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "A",
                    QualityScore = 20,
                    ChainHeight = bestBlockCount,
                    TasksAssignedCount = rnd.Next(1000)
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "B",
                    QualityScore = 100,
                    ChainHeight = bestBlockCount,
                    TasksAssignedCount = rnd.Next(1000)
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "C",
                    QualityScore = 150,
                    ChainHeight = bestBlockCount,
                    TasksAssignedCount = rnd.Next(1000)
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "D",
                    QualityScore = 120,
                    ChainHeight = bestBlockCount,
                    TasksAssignedCount = rnd.Next(1000)
                },
                new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = "E",
                    QualityScore = 90,
                    ChainHeight = bestBlockCount,
                    TasksAssignedCount = rnd.Next(1000)
                },
            };

            // Use the assignment strategy to assign tasks to peers.
            Dictionary<PullerDownloadAssignments.PeerInformation, List<int>> assignments = PullerDownloadAssignments.AssignBlocksToPeers(requiredBlockHeights, availablePeersInformation);

            // Check the assignment is valid per our requirements.
            int tasksAssigned = 0;
            Assert.Equal(availablePeersInformation.Count, assignments.Count);
            foreach (KeyValuePair<PullerDownloadAssignments.PeerInformation, List<int>> kvp in assignments)
            {
                PullerDownloadAssignments.PeerInformation peer = kvp.Key;
                List<int> assignedBlockHeights = kvp.Value;
                tasksAssigned += assignedBlockHeights.Count;

                switch ((string)peer.PeerId)
                {
                    case "A":
                    case "E":
                        // Peers A and E should not get work.
                        Assert.True((assignedBlockHeights.Count == 0) || (assignedBlockHeights.Max() >= medianBlockHeight));
                        break;

                    case "B":
                    case "C":
                    case "D":
                        // PeerS B, C, D can be assigned anything.
                        break;

                    default:
                        // This should never occur.
                        Assert.True(false, "Invalid peer ID.");
                        break;
                }
            }
            Assert.Equal(requiredBlockHeights.Count, tasksAssigned);
        }

        /// <summary>
        /// Same as <see cref="AssignBlocksToPeersWithNodesWithLowQualityProtectsLowerHalfRequestsFromBeingAssignedToPoorQualityNodes"/>
        /// except that we generate a large number of peers with random qualities.
        /// </summary>
        [Fact]
        public void AssignBlocksToPeersWithManyNodesWithLowQualityProtectsLowerHalfRequestsFromBeingAssignedToPoorQualityNodes()
        {
            Random rnd = new Random();
            int ourBlockCount = rnd.Next(1000) + 1;
            int bestBlockCount = rnd.Next(1000) + ourBlockCount + 1;

            // Create list of numbers ourBlockCount + 1 to bestBlockCount and shuffle it.
            List<int> requiredBlockHeights = new List<int>();
            for (int i = ourBlockCount + 1; i <= bestBlockCount; i++)
                requiredBlockHeights.Add(i);

            requiredBlockHeights = requiredBlockHeights.OrderBy(a => rnd.Next()).ToList();
            int medianBlockHeight = requiredBlockHeights.Median();

            // Randomly generate peers and count median quality.
            List<PullerDownloadAssignments.PeerInformation> availablePeersInformation = new List<PullerDownloadAssignments.PeerInformation>();
            int peerCount = rnd.Next(1000) + 1;
            List<double> qualities = new List<double>();
            for (int i = 0; i < peerCount; i++)
            {
                var peerInfo = new PullerDownloadAssignments.PeerInformation()
                {
                    PeerId = i.ToString(),
                    QualityScore = rnd.NextDouble() * (QualityScore.MaxScore - QualityScore.MinScore) + QualityScore.MinScore,
                    ChainHeight = bestBlockCount,
                    TasksAssignedCount = rnd.Next(1000)
                };
                qualities.Add(peerInfo.QualityScore);
                availablePeersInformation.Add(peerInfo);
            };

            double qualityMedian = qualities.Median();

            // Use the assignment strategy to assign tasks to peers.
            Dictionary<PullerDownloadAssignments.PeerInformation, List<int>> assignments = PullerDownloadAssignments.AssignBlocksToPeers(requiredBlockHeights, availablePeersInformation);

            // Check the assignment is valid per our requirements.
            int tasksAssigned = 0;
            Assert.Equal(availablePeersInformation.Count, assignments.Count);
            foreach (KeyValuePair<PullerDownloadAssignments.PeerInformation, List<int>> kvp in assignments)
            {
                PullerDownloadAssignments.PeerInformation peer = kvp.Key;
                List<int> assignedBlockHeights = kvp.Value;
                tasksAssigned += assignedBlockHeights.Count;

                // If the peer is poor, it must not get bottom half work assignment.
                if (peer.QualityScore < qualityMedian)
                    Assert.True((assignedBlockHeights.Count == 0) || (assignedBlockHeights.Max() >= medianBlockHeight));
            }
            Assert.Equal(requiredBlockHeights.Count, tasksAssigned);
        }
    }
}
