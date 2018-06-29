using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling2;
using Stratis.Bitcoin.P2P.Peer;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling2
{
    public class BlockPullerTests
    {
        private readonly BlockPullerTestsHelper helper;

        private readonly ExtendedBlockPuller puller;

        public BlockPullerTests()
        {
            this.helper = new BlockPullerTestsHelper();
            this.puller = this.helper.Puller;
        }

        [Fact]
        public async Task CanInitializeAndDisposeAsync()
        {
            this.puller.Initialize();

            // Let dequeue and stalling loops start.
            await Task.Delay(1000);

            this.puller.Dispose();
        }

        /// <summary>
        /// Make sure average block size was initially set to 0. Peer A claims 2 blocks and presents headers.
        /// After that he is assigned to download them and he delivers them. One block is 200 bytes and another
        /// is 400 bytes. Make sure that the average block size is set to 300. 
        /// </summary>
        [Fact]
        public async Task TotalSpeedOfAllPeersBytesPerSec_CalculatedCorrectlyAsync()
        {
            Assert.Equal(0, this.puller.GetAverageBlockSizeBytes());
            Assert.Equal(0, this.puller.GetTotalSpeedOfAllPeersBytesPerSec());

            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(2);
            
            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();

            // Make sure jobs were assigned.
            foreach (ChainedHeader chainedHeader in headers)
                Assert.True(this.puller.AssignedDownloadsByHash.ContainsKey(chainedHeader.HashBlock));

            this.puller.PushBlock(headers[0].HashBlock, this.helper.GenerateBlock(200), behavior.AttachedPeer.Connection.Id);
            this.puller.PushBlock(headers[1].HashBlock, this.helper.GenerateBlock(400), behavior.AttachedPeer.Connection.Id);
            
            double averageSize = this.puller.GetAverageBlockSizeBytes();
            Assert.True(this.helper.DoubleEqual(300, averageSize));
        }

        /// <summary>
        /// Connect peer A. Call OnIbdStateChanged and make sure that peer's behavior was updated about IBD state being changed.
        /// </summary>
        [Fact]
        public void OnIbdStateChanged_CallsBehaviors()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            ChainedHeader header = this.helper.CreateChainedHeader();

            this.puller.NewPeerTipClaimed(peer, header);

            this.puller.OnIbdStateChanged(false);
            Assert.False(behavior.ProvidedIbdState);

            this.puller.OnIbdStateChanged(true);
            Assert.True(behavior.ProvidedIbdState);

            this.puller.OnIbdStateChanged(false);
            Assert.False(behavior.ProvidedIbdState);
        }

        /// <summary>
        /// Connect 3 peers. Setup avg speed of them as: max / 2, max, max * 2.  When in IBD there are no speed limitations and all should have different quality scores,
        /// when not in IBD last 2 should have quality score of 1.
        /// </summary>
        [Fact]
        public void OnIbdStateChanged_AffectsQualityScore()
        {
            var peers = new List<INetworkPeer>();
            var behaviors = new List<ExtendedBlockPullerBehavior>();
            ChainedHeader header = this.helper.CreateChainedHeader();

            for (int i = 0; i < 3; i++)
            {
                INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
                peers.Add(peer);
                behaviors.Add(behavior);

                this.puller.NewPeerTipClaimed(peer, header);
            }

            int maxSpeedWhenNotIbd = this.puller.PeerSpeedLimitWhenNotInIbdBytesPerSec;

            behaviors[0].AddSample(maxSpeedWhenNotIbd / 2, 1);
            behaviors[1].AddSample(maxSpeedWhenNotIbd, 1);
            behaviors[2].AddSample(maxSpeedWhenNotIbd * 2, 1);

            this.puller.OnIbdStateChanged(true);

            // After recalculation all scores should be different.
            for (int i = 0; i < peers.Count; i++)
                this.puller.RecalculateQualityScoreLocked(behaviors[i], peers[i].Connection.Id);

            Assert.True(behaviors[0].QualityScore < behaviors[1].QualityScore && behaviors[1].QualityScore < behaviors[2].QualityScore);

            this.puller.OnIbdStateChanged(false);

            // After recalculation last 2 peers should hit the max bound and their scores should be equal.
            for (int i = 0; i < peers.Count; i++)
                this.puller.RecalculateQualityScoreLocked(behaviors[i], peers[i].Connection.Id);

            Assert.Equal(behaviors[1].QualityScore, behaviors[2].QualityScore);
            Assert.True(behaviors[0].QualityScore < behaviors[1].QualityScore);

            this.puller.OnIbdStateChanged(true);

            // Back to IBD- no speed limits.
            for (int i = 0; i < peers.Count; i++)
                this.puller.RecalculateQualityScoreLocked(behaviors[i], peers[i].Connection.Id);

            Assert.True(behaviors[0].QualityScore < behaviors[1].QualityScore && behaviors[1].QualityScore < behaviors[2].QualityScore);
        }

        /// <summary>
        /// Call NewPeerTipClaimed, make sure that pullerBehaviorsByPeerId is updated, make sure that PullerBehavior.Tip is set.
        /// </summary>
        [Fact]
        public void NewPeerTipClaimed_StructuresAreUpdated()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            ChainedHeader header = this.helper.CreateChainedHeader();

            Assert.Empty(this.puller.PullerBehaviorsByPeerId);

            this.puller.NewPeerTipClaimed(peer, header);

            Assert.True(this.puller.PullerBehaviorsByPeerId.ContainsKey(peer.Connection.Id));
            Assert.Equal(header, behavior.Tip);
        }

        /// <summary>
        /// Call NewPeerTipClaimed on a peer that can't send blocks (doesn't support the requirements) and make sure it's not added to pullerBehaviorsByPeerId.
        /// </summary>
        [Fact]
        public void NewPeerTipClaimed_PeerDoesntSupportRequirments_StructuresNotUpdated()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior, notSupportedVersion: true);
            ChainedHeader header = this.helper.CreateChainedHeader();

            this.puller.NewPeerTipClaimed(peer, header);

            Assert.Empty(this.puller.PullerBehaviorsByPeerId);
        }

        /// <summary>
        /// Call PeerDisconnected an a peer that wasn't connected- nothing happens.
        /// Call it on a peer that did exist- it's key is removed from pullerBehaviorsByPeerId.
        /// </summary>
        [Fact]
        public void PeerDisconnected_OnPeerThatPullerIsNotAwereOf_NothingHappens()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            ChainedHeader header = this.helper.CreateChainedHeader();

            this.puller.NewPeerTipClaimed(peer, header);

            this.puller.PeerDisconnected(int.MaxValue);

            Assert.True(this.puller.PullerBehaviorsByPeerId.ContainsKey(peer.Connection.Id));
            Assert.Single(this.puller.PullerBehaviorsByPeerId);
        }

        /// <summary>
        /// Create a chain and let 2 peers claim it. Connect peer 1. Assign all the blocks to peer 1. Connect peer 2.
        /// Call PeerDisconnected on PeerId 1, make sure all blocks are reassigned to peer 2.
        /// </summary>
        [Fact]
        public async Task PeerDisconnected_AllDownloadJobsAreReassignedAsync()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(10);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();
            
            // Make sure all jobs were assigned to 1.
            foreach (ChainedHeader chainedHeader in headers)
                Assert.True(this.puller.AssignedDownloadsByHash.ContainsKey(chainedHeader.HashBlock));

            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            this.puller.PeerDisconnected(peer1.Connection.Id);

            // Make sure all assignments went to reassign queue as a single job.
            Assert.Single(this.puller.ReassignedJobsQueue);

            DownloadJob reassignedJob = this.puller.ReassignedJobsQueue.Peek();

            foreach (ChainedHeader chainedHeader in headers)
                Assert.True(reassignedJob.Headers.Exists(x => x == chainedHeader));

            await this.puller.AssignDownloadJobsAsync();

            // All should be assigned to peer 2.
            Assert.Equal(headers.Count, this.puller.AssignedDownloadsByHash.Count);
            Assert.Equal(headers.Count, this.puller.AssignedDownloadsByHash.Values.Count(x => x.PeerId == peer2.Connection.Id));
        }
       
        /// <summary>
        /// Create a chain and let 1 peer claim it. Assign all the blocks to peer 1. Peer 2 connects but it is on a different chain (no blocks in common except for genesis).
        /// Call PeerDisconnected on PeerId 1, make sure callback is called with null for all blocks that were requested. Maker sure that nothing is assigned to peer 2.
        /// </summary>
        [Fact]
        public async Task PeerDisconnected_AnotherPeerThatClaimsDifferentChainAssignedNothingAsync()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> peer1Headers = this.helper.CreateConsequtiveHeaders(10);
            List<ChainedHeader> peer2Headers = this.helper.CreateConsequtiveHeaders(5);

            this.puller.SetMaxBlocksBeingDownloaded(20);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());

            this.puller.RequestBlocksDownload(peer1Headers);

            await this.puller.AssignDownloadJobsAsync();

            // make sure all jobs were assigned to 1.
            foreach (ChainedHeader chainedHeader in peer1Headers)
                Assert.True(this.puller.AssignedDownloadsByHash.ContainsKey(chainedHeader.HashBlock));
            
            this.puller.PeerDisconnected(peer1.Connection.Id);

            // Make sure all assignments went to reassign queue as a single job.
            Assert.Single(this.puller.ReassignedJobsQueue);

            Assert.Empty(this.helper.CallbacksCalled);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Empty(this.puller.ReassignedJobsQueue);
            Assert.Empty(this.puller.AssignedDownloadsByHash);

            // Callbacks called with null.
            Assert.True(this.helper.CallbacksCalled.All(x => x.Value == null));
            Assert.Equal(peer1Headers.Count, this.helper.CallbacksCalled.Count);

            foreach (ChainedHeader chainedHeader in peer1Headers)
                Assert.True(this.helper.CallbacksCalled.ContainsKey(chainedHeader.HashBlock));
        }

        /// <summary>
        /// There are no peers. Call RequestBlocksDownload with 2 headers. Make sure that DownloadJobsQueue is updated and signal is set.
        /// Call process queue and make sure callback is called for each header with null.
        /// </summary>
        [Fact]
        public async Task RequestBlocksDownload_WhileThereAreNoPeers_JobFailedAsync()
        {
            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(2);

            Assert.Empty(this.puller.DownloadJobsQueue);
            Assert.Empty(this.helper.CallbacksCalled);
            Assert.False(this.puller.ProcessQueuesSignal.IsSet);

            this.puller.RequestBlocksDownload(headers);

            // Headers were added to the jobs queue.
            Assert.Single(this.puller.DownloadJobsQueue);
            Assert.Equal(2, this.puller.DownloadJobsQueue.Peek().Headers.Count);
            Assert.True(this.puller.ProcessQueuesSignal.IsSet);

            await this.puller.AssignDownloadJobsAsync();

            // Callbacks are called with null.
            foreach (ChainedHeader chainedHeader in headers)
                Assert.Null(this.helper.CallbacksCalled[chainedHeader.HashBlock]);
        }

        /// <summary>
        /// There is 1 peer claiming the chain. Call RequestBlocksDownload with a header from that chain. Call process queue and make sure (mock it) that
        /// PeerBehavior.RequestBlocksAsync throws OperationCanceledException (mock it).
        /// Make sure that all headers are added to reassign queue and peer is removed from pullerBehaviorsByPeerId.
        /// Try to assign jobs again and make sure that callbacks are called with null.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        [Fact]
        public async Task RequestBlocksDownload_AssignedPeerThrows_JobIsFailedAndPeerDisconnectedAsync()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);

            behavior.ShouldThrowAtRequestBlocksAsync = true;

            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(5);
            
            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            Assert.Empty(this.helper.CallbacksCalled);
            this.puller.PullerBehaviorsByPeerId.Should().HaveCount(1);

            await this.puller.AssignDownloadJobsAsync();

            this.puller.PullerBehaviorsByPeerId.Should().HaveCount(0);
            this.puller.ReassignedJobsQueue.Should().HaveCount(1);
            Assert.Equal(headers.Count, this.puller.ReassignedJobsQueue.Peek().Headers.Count);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Equal(headers.Count, this.helper.CallbacksCalled.Count);
            Assert.Equal(headers.Count, this.helper.CallbacksCalled.Values.Count(x => x == null));
        }
        
        /// <summary>
        /// 2 peers claim 2 different chains. 10 blocks from chain 1 are requested. Make sure all blocks are assigned to peer 1, distributedHashes contains same
        /// amount of items as there were hashes. Check that the return value match the hashes that were supposed to be distributed.
        /// </summary>
        [Fact]
        public void DistributeHeaders_BetweenTwoPeersWhereOneIsOnADifferentChain()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> peer1Headers = this.helper.CreateConsequtiveHeaders(10);
            List<ChainedHeader> peer2Headers = this.helper.CreateConsequtiveHeaders(5);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());
            
            var job = new DownloadJob() {Headers = new List<ChainedHeader>(peer1Headers), Id = 1};
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);
            
            // make sure all jobs were assigned to 1.
            foreach (ChainedHeader chainedHeader in peer1Headers)
                Assert.True(assignedDownloads.Exists(x => x.Header == chainedHeader));

            Assert.Equal(peer1Headers.Count, assignedDownloads.Count);
        }

        /// <summary>
        /// 2 peers claim same chain. 1 random hash is asked and no peer claim it. Make sure failedHashes contains this hash.
        /// </summary>
        [Fact]
        public void DistributeHeaders_NoPeerClaimTheChain()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(10);
            ChainedHeader unclaimedHeader = this.helper.CreateChainedHeader();

            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>() { unclaimedHeader }, Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Equal(failedHashes.First(), unclaimedHeader.HashBlock);
            Assert.Single(failedHashes);
            Assert.Empty(assignedDownloads);
        }

        /// <summary>
        /// 2 peers claim same chain, they have equal quality score. 10000 blocks requested. Make sure blocks are distributed between 2 peers evenly (approximately).
        /// </summary>
        [Fact]
        public void DistributeHeaders_EvenlyDistributed()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(10000);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);
            
            Assert.Empty(failedHashes);
            Assert.Equal(headers.Count, assignedDownloads.Count);

            int elipson = Math.Abs(assignedDownloads.Count(x => x.PeerId == peer1.Connection.Id) - assignedDownloads.Count(x => x.PeerId == peer2.Connection.Id));
            
            // Amount of jobs assigned to peer 1 shouldn't be more than 10% different comparing to amount assigned to peer 2.
            Assert.True(elipson < headers.Count * 0.1);
        }

        /// <summary>
        /// There are 2 peers. One is on chain which is 1000 blocks. 2nd is on chain which forks from peer1 chain at block 500 and goes to 1000b.
        /// Hashes from chain A are requested. Make sure that hashes 500 - 1000 assigned only to peer A, hashes 0-500 are distributed between peer A and B.
        /// </summary>
        [Fact]
        public void DistributeHeaders_OnePeerForksAtSomePoint()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> peer1Headers = this.helper.CreateConsequtiveHeaders(1000);
            List<ChainedHeader> peer2Headers = this.helper.CreateConsequtiveHeaders(500, peer1Headers[500]);
            
            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());
            
            var job = new DownloadJob() { Headers = new List<ChainedHeader>(peer1Headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Empty(failedHashes);
            Assert.Equal(peer1Headers.Count, assignedDownloads.Count);

            int elipson = Math.Abs(assignedDownloads.Take(500).Count(x => x.PeerId == peer1.Connection.Id) - assignedDownloads.Take(500).Count(x => x.PeerId == peer2.Connection.Id));
            Assert.True(elipson < 50);
            
            Assert.True(assignedDownloads.Skip(501).All(x => x.PeerId == peer1.Connection.Id));
        }
        
        /// <summary>
        /// There are 2 peers claiming same chain. Peer A has 1 quality score. Peer B has 0.1 quality score.
        /// Some hashes are distributed, make sure that peer B is assigned to just a few headers.
        /// </summary>
        [Fact]
        public void DistributeHeaders_DownloadsAreDistributedAccordingToQualityScore()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);

            behavior1.OverrideQualityScore = 1;
            behavior2.OverrideQualityScore = 0.1;

            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(10000);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);
            
            double margin = (double)assignedDownloads.Count(x => x.PeerId == peer1.Connection.Id) / assignedDownloads.Count(x => x.PeerId == peer2.Connection.Id);

            // Peer A is expected to get 10 times more than peer B. 7 is used to avoid false alarms when randomization is too lucky.
            Assert.True(margin > 7);
        }

        /// <summary>
        /// There are 10 peers. 5 of them claim chain A, 5 claim chain B (all peers are mixed in the list).
        /// Both chains are 10 000 blocks long. Chain B has a fork point against chain A at 5000.
        /// Distribute chain A and make sure that 5 peers get no blocks assigned after 5000.
        /// </summary>
        [Fact]
        public void DistributeHeaders_WithALotOfPeersAndForkInTheMiddle()
        {
            var peers = new List<INetworkPeer>();
            for (int i = 0; i < 10; i++)
            {
                INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
                peers.Add(peer);
            }

            this.Shuffle(peers);

            List<ChainedHeader> chainA = this.helper.CreateConsequtiveHeaders(10000);
            List<ChainedHeader> chainB = this.helper.CreateConsequtiveHeaders(5000, chainA[5000]);

            var peerIdsClaimingA = new HashSet<int>();

            for (int i = 0; i < peers.Count; i++)
            {
                ChainedHeader tip = i >= 5 ? chainA.Last() : chainB.Last();

                if (i >= 5)
                    peerIdsClaimingA.Add(peers[i].Connection.Id);

                this.puller.NewPeerTipClaimed(peers[i], tip);
            }

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(chainA), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Empty(failedHashes);
            Assert.Equal(chainA.Count, assignedDownloads.Count);

            var peerIds = new HashSet<int>();

            foreach (AssignedDownload assignedDownload in assignedDownloads.Skip(5001))
                peerIds.Add(assignedDownload.PeerId);
            
            Assert.Equal(5, peerIds.Count);
            Assert.Equal(peerIdsClaimingA.Count, peerIds.Count);

            foreach (int id in peerIdsClaimingA)
                Assert.Contains(id, peerIds);
        }

        /// <summary>
        /// We are asked for 100 hashes. emptySlots is 50. Make sure that only 50 hashes were assigned. Make sure that 50 headers are left in the job.
        /// </summary>
        [Fact]
        public void DistributeHeaders_LimitedByEmptySlots()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            
            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(100);

            this.puller.NewPeerTipClaimed(peer, headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, 50);

            Assert.Equal(50, assignedDownloads.Count);
            Assert.Equal(50, job.Headers.Count);
        }

        /// <summary>
        /// 100 hashes are distributed but there is only one peer claiming first 50. Make sure that 50 are assigned and 50 are failed.
        /// </summary>
        [Fact]
        public void DistributeHeaders_PartOfTheJobFailed()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);

            List<ChainedHeader> headers = this.helper.CreateConsequtiveHeaders(100);

            this.puller.NewPeerTipClaimed(peer, headers[49]);

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Equal(50, assignedDownloads.Count);
            Assert.Equal(50, failedHashes.Count);
        }
        
        /// <summary>
        /// Call Initialize, signal queue processing, wait until it is reset, make sure no structures were updated. 
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_CalledOnEmptyQueuesAsync()
        {
            this.puller.Initialize();

            this.puller.ProcessQueuesSignal.Set();

            await Task.Delay(500);

            this.puller.Dispose();

            Assert.Empty(this.puller.AssignedDownloadsByHash);
            Assert.Empty(this.puller.DownloadJobsQueue);
        }

        /// <summary>
        /// Add something to DownloadJobsQueue. Make sure we have less than 10% of empty slots, start AssignerLoop and make sure no jobs were assigned.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_LessThanThresholdSlotsAsync()
        {
            this.puller.SetMaxBlocksBeingDownloaded(100);

            // Fill AssignedDownloadsByHash to ensure that we nave just 5 slots.
            for (int i = 0; i < 95; i++)
                this.puller.AssignedDownloadsByHash.Add(RandomUtils.GetUInt64(), new AssignedDownload());

            await this.puller.AssignDownloadJobsAsync();

            // If nothing was assigned- no callbacks with null are called.
            Assert.Empty(this.helper.CallbacksCalled);
        }

        /// <summary>
        /// Empty slots = 10. 3 Jobs in the queue (5 elements, 4 elements and 10 elements). Call AssignDownloadJobsAsync and make sure that first 2 jobs were consumed and
        /// the last job has only 1 item consumed (9 items left). Make sure that peer behaviors were called to request consumed hashes. AssignedDownloadsByHash was properly modified.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_SlotsLimitStopsConsumptionAsync()
        {
            // Set 10 empty slots.
            this.puller.SetMaxBlocksBeingDownloaded(10);

            var jobSizes = new[] {5, 4, 10};

            var behaviors = new List<ExtendedBlockPullerBehavior>(jobSizes.Length);

            foreach (int jobSize in jobSizes)
            {
                List<ChainedHeader> hashes = this.helper.CreateConsequtiveHeaders(jobSize);
                INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
                behaviors.Add(behavior);

                this.puller.NewPeerTipClaimed(peer, hashes.Last());
                this.puller.RequestBlocksDownload(hashes);
            }
            
            await this.puller.AssignDownloadJobsAsync();

            Assert.Single(this.puller.DownloadJobsQueue);
            Assert.Equal(9, this.puller.DownloadJobsQueue.Peek().Headers.Count);


            Assert.Equal(jobSizes[0], behaviors[0].RequestedHashes.Count);
            Assert.Equal(jobSizes[1], behaviors[1].RequestedHashes.Count);

            Assert.Single(behaviors[2].RequestedHashes);
            Assert.Empty(this.helper.CallbacksCalled);

            Assert.Equal(10, this.puller.AssignedDownloadsByHash.Count);
        }

        /*
        /// <summary>
        /// Assign some headers, peers that claimed them are disconnected. Make sure that callbacks for those blocks are called with null, make sure job removed from the structures.
        /// </summary>
        [Fact]
        public void AssignDownloadJobs_PeerDisconnectedAndJobFailed()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// Assign some headers and when peer.BlockPullerBehaviour.Download is called- throw for one peer. Make sure that all hashes that belong to that peer are
        /// reassigned to someone else and that we don't have this peer in our structures.
        /// </summary>
        [Fact]
        public void AssignDownloadJobs_PeerThrowsAndHisAssignmentAreReassigned()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// We have 2 hashes in reassign queue and 2 in assign queue. There is 1 empty slot. Make sure that reassign queue is consumed fully and assign queue has only 1 item left.
        /// </summary>
        [Fact]
        public void AssignDownloadJobs_ReassignQueueIgnoresEmptySlots()
        {

            throw new NotImplementedException();
        }
        
        /// <summary>
        /// There are 2 peers with quality score of 1 and 0.5. Peer 2 is asked 1 block. After N seconds he doesn't deliver and it's reassigned.
        /// Make sure quality score is decreased and peer sample is added (and before that time it shouldn't be changed).
        /// </summary>
        [Fact]
        public void Stalling_CanReassignAndDecreaseQualityScore()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// We are not in IBD, Peer is asked for 10 blocks, peer failed to deliver all of them. They are reassigned, make sure that peer have 1
        /// (calculated from the constant value of % of samples to penalize) sample with 0kb size added.
        /// </summary>
        [Fact]
        public void Stalling_WhenPeerFailsToDeliverOnlyFixedAmountOfSamesIsSetToZero()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// We are not in IBD, Ask 1 peer for (important block margin constant) 10 blocks. After that ask 2 new peers for 20 blocks. None of the peer
        /// deliver anything, peer 1's blocks are reassigned and penalty is applied, penalty is not applied on other peers because their assignment
        /// is not important. Make sure that headers that are released are in reassign job queue (don't start async loop so they are not consumed immediately).
        /// </summary>
        [Fact]
        public void Stalling_DoesntAffectPeersThatFailedToDeliverNotImportantBlocks()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// We are not in IBD, ask 1 peer for 10 blocks and another peer for 2 blocks. First peer don't deliver- reassign his blocks to him again.
        /// Advance CT by 1. None of them deliver. Make sure both peers are penalized.
        /// </summary>
        [Fact]
        public void Stalling_PenaltyIsAppliedWhenBlockBecomesImportantAndPeerStalls()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// Dont start assigner loop. Assign following headers (1 header = 1 job) for 20 headers but ask those in random order.
        /// Create 100 peers with same quality score. Assign jobs to peers. No one delivers. Make sure that peers that were
        /// assigned important jobs are penalized, others are not.
        /// </summary>
        [Fact]
        public void Stalling_ImportantHeadersAreReleased()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// Request some hashes (RequestBlockDownload). Call push blocks. Make sure callback is called. Make sure that assignment is
        /// removed from AssignedDownloads. Make sure quality score is updated. Make sure max blocks being downloaded is recalculated.
        /// Make sure TotalSpeedOfAllPeersBytesPerSec and AvgBlockSize are recalculated and new values for the circular array are added. Make sure that signal is set.
        /// </summary>
        [Fact]
        public void PushBlock_AppropriateStructuresAreUpdated()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// Push block that wasn't requested for download- nothing happens, no structure is updated.
        /// </summary>
        [Fact]
        public void PushBlock_OnBlockThatWasntRequested_NothingHappens()
        {

            throw new NotImplementedException();
        }

        /// <summary>
        /// Push block that was requested from another peer- nothing happens, no structure is updated. 
        /// </summary>
        [Fact]
        public void PushBlock_ByPeerThatWereNotAssignedToIt_NothingHappens()
        {

            throw new NotImplementedException();
        }
        */

        private Random random = new Random();

        private void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = this.random.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
