using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.BlockPulling
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
        public void RequestPeerServices_PeersThatDontSupportNewServicesAreRemoved()
        {
            Mock<INetworkPeer> peer1 = this.helper.CreatePeerMock(out ExtendedBlockPullerBehavior behavior1);
            Mock<INetworkPeer> peer2 = this.helper.CreatePeerMock(out ExtendedBlockPullerBehavior behavior2);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5);

            this.puller.NewPeerTipClaimed(peer1.Object, headers.Last());
            this.puller.NewPeerTipClaimed(peer2.Object, headers.Last());

            Assert.Equal(2, this.puller.PullerBehaviorsByPeerId.Count);

            VersionPayload version = new NetworkPeerConnectionParameters().CreateVersion(new IPEndPoint(1, 1), new IPEndPoint(1, 1),
                KnownNetworks.StratisMain, new DateTimeProvider().GetTimeOffset());

            version.Services = NetworkPeerServices.Network | NetworkPeerServices.NODE_WITNESS;

            peer1.SetupGet(x => x.PeerVersion).Returns(version);

            this.puller.RequestPeerServices(NetworkPeerServices.NODE_WITNESS);

            Assert.Equal(1, this.puller.PullerBehaviorsByPeerId.Count);
        }

        [Fact]
        public void RequestPeerServices_PeersThatDontSupportNewServicesAreNotAdded()
        {
            this.puller.RequestPeerServices(NetworkPeerServices.NODE_WITNESS);

            Mock<INetworkPeer> peer1 = this.helper.CreatePeerMock(out ExtendedBlockPullerBehavior behavior1);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5);

            this.puller.NewPeerTipClaimed(peer1.Object, headers.Last());

            Assert.Equal(0, this.puller.PullerBehaviorsByPeerId.Count);
        }

        [Fact]
        public async Task CanInitializeAndDisposeAsync()
        {
            this.puller.Initialize((hash, block, peerId) => { });

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
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            Assert.Equal(0, this.puller.GetAverageBlockSizeBytes());
            Assert.Equal(0, this.puller.GetTotalSpeedOfAllPeersBytesPerSec());

            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);

            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();

            // Make sure job was assigned.
            foreach (ChainedHeader chainedHeader in headers)
                Assert.True(this.puller.AssignedDownloadsByHash.ContainsKey(chainedHeader.HashBlock));

            this.puller.PushBlock(headers[0].HashBlock, this.helper.GenerateBlock(200), behavior.AttachedPeer.Connection.Id);
            this.puller.PushBlock(headers[1].HashBlock, this.helper.GenerateBlock(400), behavior.AttachedPeer.Connection.Id);

            double averageSize = this.puller.GetAverageBlockSizeBytes();
            Assert.True(this.helper.DoubleEqual(300, averageSize));
        }

        /// <summary>
        /// Call <see cref="BlockPuller.NewPeerTipClaimed"/>, make sure that internal structures of the puller are updated,
        /// make sure that <see cref="BlockPullerBehavior.Tip"/> is set.
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
        /// Call <see cref="BlockPuller.NewPeerTipClaimed"/> on a peer that can't send blocks (doesn't support the requirements)
        /// and make sure it's not added to the block puller's structures.
        /// </summary>
        [Fact]
        public void NewPeerTipClaimed_PeerDoesntSupportRequirments_StructuresNotUpdated()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior, notSupportedVersion: true);
            ChainedHeader header = this.helper.CreateChainedHeader();

            this.puller.NewPeerTipClaimed(peer, header);

            Assert.Empty(this.puller.PullerBehaviorsByPeerId);
            Assert.Null(behavior.Tip);
        }

        /// <summary>
        /// Call <see cref="BlockPuller.PeerDisconnected"/> an a peer that wasn't connected, nothing happens.
        /// Call it on a peer that did exist, it's key is removed from inner structures.
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

            this.puller.PeerDisconnected(peer.Connection.Id);
            Assert.Empty(this.puller.PullerBehaviorsByPeerId);
        }

        /// <summary>
        /// Create a chain and let 2 peers claim it. Connect peer 1. Assign all the blocks to peer 1. Connect peer 2.
        /// Call <see cref="BlockPuller.PeerDisconnected"/> on peer 1, make sure all blocks are reassigned to peer 2.
        /// </summary>
        [Fact]
        public async Task PeerDisconnected_AllDownloadJobsAreReassignedAsync()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();

            // Make sure all jobs were assigned to 1.
            foreach (ChainedHeader chainedHeader in headers)
                Assert.True(this.puller.AssignedDownloadsByHash.ContainsKey(chainedHeader.HashBlock));

            Assert.Equal(headers.Count, this.puller.AssignedDownloadsByHash.Count(x => x.Value.PeerId == peer1.Connection.Id));

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
            Assert.Empty(this.puller.ReassignedJobsQueue);
        }

        /// <summary>
        /// Create a chain and let 1 peer claim it. Assign all the blocks to peer 1. Peer 2 connects but it is on a different chain (no blocks in common except for genesis).
        /// Call <see cref="BlockPuller.PeerDisconnected"/> on peer 1, make sure callback is called with null for all blocks that were requested.
        /// Make sure that nothing is assigned to peer 2.
        /// </summary>
        [Fact]
        public async Task PeerDisconnected_AnotherPeerThatClaimsDifferentChainAssignedNothingAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> peer1Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10);
            List<ChainedHeader> peer2Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5);

            this.puller.SetMaxBlocksBeingDownloaded(20);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());

            this.puller.RequestBlocksDownload(peer1Headers);

            await this.puller.AssignDownloadJobsAsync();

            // Make sure all jobs were assigned to peer 1.
            foreach (ChainedHeader chainedHeader in peer1Headers)
                Assert.True(this.puller.AssignedDownloadsByHash.ContainsKey(chainedHeader.HashBlock));

            Assert.Equal(peer1Headers.Count, this.puller.AssignedDownloadsByHash.Count(x => x.Value.PeerId == peer1.Connection.Id));

            this.puller.PeerDisconnected(peer1.Connection.Id);

            // Make sure all assignments went to reassign queue as a single job.
            Assert.Single(this.puller.ReassignedJobsQueue);

            Assert.Empty(this.helper.CallbacksCalled);

            // Try to reassign.
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
        /// There are no peers. Call <see cref="BlockPuller.RequestBlocksDownload"/> with 2 headers. Make sure that download queue is updated and signal is set.
        /// Call process queue and make sure callback is called for each header with <c>null</c>.
        /// </summary>
        [Fact]
        public async Task RequestBlocksDownload_WhileThereAreNoPeers_JobFailedAsync()
        {
            this.puller.SetCallback((hash, block, pereId) => { this.helper.CallbacksCalled.Add(hash, block); });

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);

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
        /// There is 1 peer claiming the chain. Call <see cref="BlockPuller.RequestBlocksDownload"/> with a header from that chain.
        /// Process queue and make sure that <see cref="BlockPullerBehavior.RequestBlocksAsync"/> throws <see cref="OperationCanceledException"/> (mock it).
        /// Make sure that all headers are added to reassign queue and peer is removed from the inner structures.
        /// Try to assign jobs again and make sure that callbacks are called with <c>null</c>.
        /// </summary>
        [Fact]
        public async Task RequestBlocksDownload_AssignedPeerThrows_JobIsFailedAndPeerDisconnectedAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);

            behavior.ShouldThrowAtRequestBlocksAsync = true;

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5);

            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            Assert.Empty(this.helper.CallbacksCalled);
            Assert.Single(this.puller.PullerBehaviorsByPeerId);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Empty(this.puller.PullerBehaviorsByPeerId);
            Assert.Single(this.puller.ReassignedJobsQueue);
            Assert.Equal(headers.Count, this.puller.ReassignedJobsQueue.Peek().Headers.Count);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Equal(headers.Count, this.helper.CallbacksCalled.Count);

            // Callbacks are called with null.
            foreach (ChainedHeader chainedHeader in headers)
                Assert.Null(this.helper.CallbacksCalled[chainedHeader.HashBlock]);
        }

        /// <summary>
        /// 2 peers claim 2 different chains. 10 blocks from chain 1 are requested. Make sure all blocks are assigned to peer 1,
        /// and inner structures are updated accordingly. Check that the distributed assignments match the hashes that were supposed to be distributed.
        /// </summary>
        [Fact]
        public void DistributeHeaders_BetweenTwoPeersWhereOneIsOnADifferentChain()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> peer1Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10);
            List<ChainedHeader> peer2Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(5);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());

            var job = new DownloadJob() {Headers = new List<ChainedHeader>(peer1Headers), Id = 1};
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            // Make sure all jobs were assigned to peer 1.
            foreach (ChainedHeader chainedHeader in peer1Headers)
                Assert.True(assignedDownloads.Exists(x => x.Header == chainedHeader));

            Assert.Equal(peer1Headers.Count, assignedDownloads.Count);
        }

        /// <summary>
        /// Peer claim some chain. 1 random hash is asked and peer doesn't claim it. Make sure hash was marked as failed.
        /// </summary>
        [Fact]
        public void DistributeHeaders_NoPeerClaimTheChain()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10);
            ChainedHeader unclaimedHeader = this.helper.CreateChainedHeader();

            this.puller.NewPeerTipClaimed(peer1, headers.Last());

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
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10000);

            behavior1.OverrideQualityScore = behavior2.OverrideQualityScore = 1;

            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Empty(failedHashes);
            Assert.Equal(headers.Count, assignedDownloads.Count);

            int epsilon = Math.Abs(assignedDownloads.Count(x => x.PeerId == peer1.Connection.Id) - assignedDownloads.Count(x => x.PeerId == peer2.Connection.Id));

            // Amount of jobs assigned to peer 1 shouldn't be more than 10% different comparing to amount assigned to peer 2.
            Assert.True(epsilon < headers.Count * 0.1);
        }

        /// <summary>
        /// There are 2 peers. One is on a chain which is 1000 blocks. 2nd is on chain which forks from peer1 chain at block 500 and goes to 1000b.
        /// Hashes from chain A are requested. Make sure that hashes 500 - 1000 assigned only to peer A, hashes 0-500 are distributed between peer A and B.
        /// </summary>
        [Fact]
        public void DistributeHeaders_OnePeerForksAtSomePoint()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            List<ChainedHeader> peer1Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(1000);
            List<ChainedHeader> peer2Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(500, peer1Headers[500 - 1]);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(peer1Headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Empty(failedHashes);
            Assert.Equal(peer1Headers.Count, assignedDownloads.Count);
            Assert.True(assignedDownloads.Take(500).Count(x => x.PeerId == peer1.Connection.Id) > 0);
            Assert.True(assignedDownloads.Take(500).Count(x => x.PeerId == peer2.Connection.Id) > 0);
            Assert.True(assignedDownloads.Skip(500).All(x => x.PeerId == peer1.Connection.Id));
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

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10000);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            double margin = (double)assignedDownloads.Count(x => x.PeerId == peer1.Connection.Id) / assignedDownloads.Count(x => x.PeerId == peer2.Connection.Id);

            // Peer A is expected to get 10 times more than peer B. 8 is used to avoid false alarms when randomization is too lucky.
            Assert.True(margin > 8);
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

            List<ChainedHeader> chainA = ChainedHeadersHelper.CreateConsecutiveHeaders(10000);
            List<ChainedHeader> chainB = ChainedHeadersHelper.CreateConsecutiveHeaders(5000, chainA[5000 - 1]);

            List<int> peerIdsClaimingA = peers.Take(5).Select(x => x.Connection.Id).ToList();

            this.Shuffle(peers);

            foreach (INetworkPeer peer in peers)
                this.puller.NewPeerTipClaimed(peer, peerIdsClaimingA.Contains(peer.Connection.Id) ? chainA.Last() : chainB.Last());

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(chainA), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Empty(failedHashes);
            Assert.Equal(chainA.Count, assignedDownloads.Count);

            var peerIds = new HashSet<int>();

            foreach (AssignedDownload assignedDownload in assignedDownloads.Skip(5000))
                peerIds.Add(assignedDownload.PeerId);

            Assert.Equal(5, peerIds.Count);
            Assert.Equal(peerIdsClaimingA.Count, peerIds.Count);

            foreach (int id in peerIdsClaimingA)
                Assert.Contains(id, peerIds);
        }

        /// <summary>
        /// We are asked for 100 hashes. We only have 50 slots empty. Make sure that only 50 hashes were assigned. Make sure that 50 headers are left in the job.
        /// </summary>
        [Fact]
        public void DistributeHeaders_LimitedByEmptySlots()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100);

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

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100);

            this.puller.NewPeerTipClaimed(peer, headers[49]);

            var job = new DownloadJob() { Headers = new List<ChainedHeader>(headers), Id = 1 };
            var failedHashes = new List<uint256>();

            List<AssignedDownload> assignedDownloads = this.puller.DistributeHeadersLocked(job, failedHashes, int.MaxValue);

            Assert.Equal(50, assignedDownloads.Count);
            Assert.Equal(50, failedHashes.Count);
        }

        /// <summary>
        /// Call <see cref="BlockPuller.Initialize"/>, signal queue processing, wait until it is reset, make sure no structures were updated.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_CalledOnEmptyQueuesAsync()
        {
            this.puller.Initialize((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            this.puller.ProcessQueuesSignal.Set();

            while (this.puller.ProcessQueuesSignal.IsSet)
                await Task.Delay(50);

            Assert.Empty(this.puller.AssignedDownloadsByHash);
            Assert.Empty(this.puller.DownloadJobsQueue);

            this.puller.Dispose();
        }

        /// <summary>
        /// Add some items to download job queue. Make sure we have less than 10% of empty slots,
        /// invoke downloads assigning and make sure no jobs were assigned.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_LessThanThresholdSlotsAsync()
        {
            this.puller.SetMaxBlocksBeingDownloaded(100);

            // Fill AssignedDownloadsByHash to ensure that we have just 5 slots.
            for (int i = 0; i < 95; i++)
                this.puller.AssignedDownloadsByHash.Add(RandomUtils.GetUInt64(), new AssignedDownload());

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10);
            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();

            // If nothing was assigned, no callbacks with null are called.
            Assert.Empty(this.helper.CallbacksCalled);
            Assert.Equal(10, this.puller.DownloadJobsQueue.Peek().Headers.Count);
        }

        /// <summary>
        /// Empty slots = 10. 3 jobs are the queue (5 elements, 4 elements and 10 elements). Trigger assignments and make sure that first 2 jobs were consumed and
        /// the last job has only 1 item consumed (9 items left). Make sure that peer behaviors were called to request consumed hashes. Puller's structures were properly modified.
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
                List<ChainedHeader> hashes = ChainedHeadersHelper.CreateConsecutiveHeaders(jobSize);
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
            this.VerifyAssignedDownloadsSortedOrder();

            Assert.True(this.puller.AssignedHeadersByPeerId[behaviors[0].AttachedPeer.Connection.Id].Count == jobSizes[0]);
            Assert.True(this.puller.AssignedHeadersByPeerId[behaviors[1].AttachedPeer.Connection.Id].Count == jobSizes[1]);
            Assert.True(this.puller.AssignedHeadersByPeerId[behaviors[2].AttachedPeer.Connection.Id].Count == 1);
        }

        /// <summary>
        /// Assign some headers, peer that claimed them is disconnected. Process the queue.
        /// Make sure that callbacks for those blocks are called with <c>null</c>, make sure job removed from the structures.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_PeerDisconnectedAndJobFailedAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100);

            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            this.puller.PeerDisconnected(peer.Connection.Id);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Equal(100, this.helper.CallbacksCalled.Count);
            foreach (ChainedHeader chainedHeader in headers)
                Assert.Null(this.helper.CallbacksCalled[chainedHeader.HashBlock]);

            Assert.Empty(this.puller.AssignedDownloadsByHash);
            Assert.Empty(this.puller.DownloadJobsQueue);
        }

        /// <summary>
        /// Assign some headers and when one of the peers is asked for a block it throws.
        /// Make sure that all hashes that belong to that peer are reassigned to someone
        /// else and that we don't have this peer in our structures.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_PeerThrowsAndHisAssignmentAreReassignedAsync()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            behavior2.ShouldThrowAtRequestBlocksAsync = true;

            this.puller.SetMaxBlocksBeingDownloaded(int.MaxValue);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(10000);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();

            int headersReassignedFromPeer2Count = headers.Count - this.puller.AssignedDownloadsByHash.Values.Count(x => x.PeerId == peer1.Connection.Id);

            Assert.Single(this.puller.ReassignedJobsQueue);
            Assert.Empty(this.puller.DownloadJobsQueue);
            Assert.Equal(headersReassignedFromPeer2Count, this.puller.ReassignedJobsQueue.Peek().Headers.Count);
            Assert.Single(this.puller.PullerBehaviorsByPeerId);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Empty(this.puller.ReassignedJobsQueue);
            Assert.Empty(this.puller.DownloadJobsQueue);
            Assert.Equal(headers.Count, this.puller.AssignedDownloadsByHash.Count);
            Assert.True(this.puller.AssignedDownloadsByHash.All(x => x.Value.PeerId == peer1.Connection.Id));
            this.VerifyAssignedDownloadsSortedOrder();
        }

        /// <summary>
        /// We have 2 hashes in reassign queue and 2 in assign queue. There is 1 empty slot.
        /// Make sure that reassign queue is consumed fully and assign queue has only 1 item left.
        /// </summary>
        [Fact]
        public async Task AssignDownloadJobs_ReassignQueueIgnoresEmptySlotsAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            behavior2.ShouldThrowAtRequestBlocksAsync = true;

            this.puller.SetMaxBlocksBeingDownloaded(int.MaxValue);

            List<ChainedHeader> peer1Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);
            List<ChainedHeader> peer2Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());

            this.puller.RequestBlocksDownload(peer2Headers);

            await this.puller.AssignDownloadJobsAsync();

            this.puller.RequestBlocksDownload(peer1Headers);

            Assert.Single(this.puller.ReassignedJobsQueue);
            Assert.Single(this.puller.DownloadJobsQueue);

            Assert.Equal(2, this.puller.ReassignedJobsQueue.Peek().Headers.Count);
            Assert.Equal(2, this.puller.DownloadJobsQueue.Peek().Headers.Count);

            // 1 empty slot.
            this.puller.SetMaxBlocksBeingDownloaded(1);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Empty(this.puller.ReassignedJobsQueue);
            Assert.Single(this.puller.DownloadJobsQueue);
            Assert.Single(this.puller.DownloadJobsQueue.Peek().Headers);
        }

        /// <summary>
        /// Ask 1 peer for <see cref="ExtendedBlockPuller.ImportantHeightMargin"/> blocks. After that ask 2nd peer for more. None of the peers
        /// deliver anything, peer 1's blocks are reassigned and penalty is applied, penalty is not applied on another peer because their assignment
        /// is not important. Make sure that headers that are released are in reassign job queue.
        /// </summary>
        [Fact]
        public async Task Stalling_DoesntAffectPeersThatFailedToDeliverNotImportantBlocksAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);

            this.puller.SetMaxBlocksBeingDownloaded(int.MaxValue);

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(this.puller.ImportantHeightMargin + 10000);

            this.puller.NewPeerTipClaimed(peer1, headers.Last());

            // Assign first ImportantHeightMargin headers to peer 1 so when stalling happens it happens only on peer 1.
            this.puller.RequestBlocksDownload(headers.Take(this.puller.ImportantHeightMargin).ToList());
            await this.puller.AssignDownloadJobsAsync();

            this.puller.NewPeerTipClaimed(peer2, headers.Last());

            this.puller.RequestBlocksDownload(headers.Skip(this.puller.ImportantHeightMargin).ToList());

            behavior1.AddSample(100, 0.1);
            behavior2.AddSample(100, 0.1);

            behavior1.RecalculateQualityScore(10);
            behavior2.RecalculateQualityScore(10);

            Assert.True(this.helper.DoubleEqual(behavior1.QualityScore, behavior2.QualityScore));

            await this.puller.AssignDownloadJobsAsync();

            // Fake assign time to avoid waiting for a long time.
            foreach (AssignedDownload assignedDownload in this.puller.AssignedDownloadsByHash.Values)
                assignedDownload.AssignedTime = (assignedDownload.AssignedTime - TimeSpan.FromSeconds(this.puller.MaxSecondsToDeliverBlock));

            Assert.Empty(this.puller.ReassignedJobsQueue);

            List<AssignedDownload> peer1Assignments = this.puller.AssignedDownloadsByHash.Values.Where(x => x.PeerId == peer1.Connection.Id).ToList();

            this.puller.CheckStalling();

            Assert.True(behavior1.QualityScore < behavior2.QualityScore);

            // Two jobs reassigned from peer 1.
            Assert.Equal(2, this.puller.ReassignedJobsQueue.Count);

            var chainedHeaders = new List<ChainedHeader>();
            foreach (DownloadJob downloadJob in this.puller.ReassignedJobsQueue)
                chainedHeaders.AddRange(downloadJob.Headers);

            Assert.True(chainedHeaders.All(x => peer1Assignments.Exists(y => y.Header == x)));
        }

        /// <summary>
        /// Don't start assigner loop. Assign following headers (1 header = 1 job) for <see cref="ExtendedBlockPuller.ImportantHeightMargin"/> * 2 headers
        /// but ask those in random order. Create a lot of peers (more than headers) with same quality score. Assign jobs to peers.
        /// No one delivers. Make sure that peers that were assigned important jobs were stalled, others are not.
        /// </summary>
        [Fact]
        public async Task Stalling_ImportantHeadersAreReleasedAsync()
        {
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(this.puller.ImportantHeightMargin * 2 + 5);

            this.helper.ChainState.ConsensusTip = headers[5];

            var peers = new List<INetworkPeer>();

            for (int i = 0; i < this.puller.ImportantHeightMargin * 5; i++)
            {
                INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
                peers.Add(peer);
                this.puller.NewPeerTipClaimed(peer, headers.Last());
            }

            this.puller.SetMaxBlocksBeingDownloaded(int.MaxValue);

            this.Shuffle(headers);
            foreach (ChainedHeader header in headers)
                this.puller.RequestBlocksDownload(new List<ChainedHeader>() { header });

            await this.puller.AssignDownloadJobsAsync();

            // Fake assign time to avoid waiting for a long time.
            foreach (AssignedDownload assignedDownload in this.puller.AssignedDownloadsByHash.Values)
                assignedDownload.AssignedTime = (assignedDownload.AssignedTime - TimeSpan.FromSeconds(this.puller.MaxSecondsToDeliverBlock));

            Assert.Empty(this.puller.ReassignedJobsQueue);

            // Peers that were assigned headers 0 to ImportantHeightMargin
            var peersWithImportantJobs = new HashSet<int>();
            var importantHeaders = new List<ChainedHeader>();

            foreach (AssignedDownload assignedDownload in this.puller.AssignedDownloadsByHash.Values)
            {
                if (assignedDownload.Header.Height <= this.helper.ChainState.ConsensusTip.Height + this.puller.ImportantHeightMargin)
                {
                    peersWithImportantJobs.Add(assignedDownload.PeerId);
                    importantHeaders.Add(assignedDownload.Header);
                }
            }

            this.puller.CheckStalling();

            Assert.True(this.puller.ReassignedJobsQueue.Count >= peersWithImportantJobs.Count);

            var chainedHeaders = new List<ChainedHeader>();
            foreach (DownloadJob downloadJob in this.puller.ReassignedJobsQueue)
                chainedHeaders.AddRange(downloadJob.Headers);

            // All important headers are reassigned.
            Assert.True(importantHeaders.All(x => chainedHeaders.Exists(y => y == x)));
        }

        /// <summary>
        /// There are 2 peers that claim different chains. Peer 1 is asked to deliver but it continuously stalls and all the jobs are
        /// still reassigned to it because it's the only peer that claims requested chain.
        /// </summary>
        [Fact]
        public async Task Stalling_PeerStallsButQualityScoreIsTheBestBecausePeerIsTheOnlyOneAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(1000);

            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            this.puller.NewPeerTipClaimed(peer1, headers.Last());
            behavior1.AddSample(100, 0.1);

            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);
            this.puller.NewPeerTipClaimed(peer2, ChainedHeadersHelper.CreateConsecutiveHeaders(10).Last());
            behavior2.AddSample(100, 0.5);

            this.puller.SetMaxBlocksBeingDownloaded(int.MaxValue);

            this.puller.RequestBlocksDownload(headers);

            for (int i = 0; i < 100; i++)
            {
                await this.puller.AssignDownloadJobsAsync();

                // Fake assign time to avoid waiting for a long time.
                foreach (AssignedDownload assignedDownload in this.puller.AssignedDownloadsByHash.Values)
                    assignedDownload.AssignedTime = (assignedDownload.AssignedTime - TimeSpan.FromSeconds(this.puller.MaxSecondsToDeliverBlock));

                this.puller.CheckStalling();
            }

            Assert.Equal(BlockPullerBehavior.MinQualityScore, behavior1.QualityScore);

            Assert.Single(this.puller.ReassignedJobsQueue);
            Assert.Equal(this.puller.ReassignedJobsQueue.Peek().Headers.Count, headers.Count);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Equal(headers.Count, this.puller.AssignedDownloadsByHash.Values.Count(x => x.PeerId == peer1.Connection.Id));
            Assert.Single(this.puller.AssignedHeadersByPeerId);
            Assert.True(this.puller.AssignedHeadersByPeerId.ContainsKey(peer1.Connection.Id));

            foreach (ChainedHeader chainedHeader in this.puller.AssignedHeadersByPeerId.First().Value)
                Assert.Contains(chainedHeader, headers);

            Assert.True(this.puller.AssignedHeadersByPeerId[peer1.Connection.Id].Count == headers.Count);
        }

        /// <summary>
        /// Request some hashes and deliver one of the blocks. Make sure callback is called, assignment is removed from puller's structures, quality score
        /// is updated, max blocks being downloaded is recalculated, total speed and average block size values are updated and the signal is set.
        /// </summary>
        [Fact]
        public async Task PushBlock_AppropriateStructuresAreUpdatedAsync()
        {
            this.puller.SetCallback((hash, block, peerId) => { this.helper.CallbacksCalled.Add(hash, block); });

            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);

            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.RequestBlocksDownload(headers);

            await this.puller.AssignDownloadJobsAsync();

            Assert.Equal(0, this.puller.GetAverageBlockSizeBytes());
            Assert.False(behavior.RecalculateQualityScoreWasCalled);

            Block blockToPush = this.helper.GenerateBlock(100);

            int oldMaxBlocksBeingDownloaded = 100;
            this.puller.SetMaxBlocksBeingDownloaded(oldMaxBlocksBeingDownloaded);

            long oldTotalSpeed = this.puller.GetTotalSpeedOfAllPeersBytesPerSec();

            this.puller.PushBlock(headers.First().HashBlock, blockToPush, peer.Connection.Id);

            Assert.Single(this.helper.CallbacksCalled);
            Assert.Equal(blockToPush, this.helper.CallbacksCalled[headers.First().HashBlock]);

            Assert.Single(this.puller.AssignedHeadersByPeerId);
            Assert.True(this.puller.AssignedHeadersByPeerId.ContainsKey(peer.Connection.Id));
            Assert.True(this.puller.AssignedHeadersByPeerId.First().Value.First() == headers.Last());
            Assert.Single(this.puller.AssignedDownloadsByHash);
            Assert.False(this.puller.AssignedDownloadsByHash.ContainsKey(headers.First().HashBlock));
            Assert.Equal(blockToPush.BlockSize.Value, this.puller.GetAverageBlockSizeBytes());
            Assert.True(this.puller.ProcessQueuesSignal.IsSet);
            Assert.True(behavior.RecalculateQualityScoreWasCalled);
            Assert.NotEqual(oldMaxBlocksBeingDownloaded, this.puller.GetMaxBlocksBeingDownloaded());
            Assert.NotEqual(oldTotalSpeed, this.puller.GetTotalSpeedOfAllPeersBytesPerSec());
        }

        /// <summary>
        /// Push block that wasn't requested for download and nothing happens, no structure is updated.
        /// </summary>
        [Fact]
        public void PushBlock_OnBlockThatWasntRequested_NothingHappens()
        {
            INetworkPeer peer = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);

            this.puller.NewPeerTipClaimed(peer, headers.Last());

            this.puller.PushBlock(this.helper.CreateChainedHeader().HashBlock, this.helper.GenerateBlock(100), 1);

            Assert.Empty(this.puller.DownloadJobsQueue);
            Assert.Empty(this.puller.ReassignedJobsQueue);
            Assert.Empty(this.puller.AssignedHeadersByPeerId);

            Assert.Empty(this.helper.CallbacksCalled);
        }

        /// <summary>
        /// Push block that was requested from another peer- nothing happens, no structure is updated, no callback is called.
        /// </summary>
        [Fact]
        public async Task PushBlock_ByPeerThatWereNotAssignedToIt_NothingHappensAsync()
        {
            INetworkPeer peer1 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior1);
            INetworkPeer peer2 = this.helper.CreatePeer(out ExtendedBlockPullerBehavior behavior2);

            List<ChainedHeader> peer1Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);
            List<ChainedHeader> peer2Headers = ChainedHeadersHelper.CreateConsecutiveHeaders(2);

            this.puller.NewPeerTipClaimed(peer1, peer1Headers.Last());
            this.puller.NewPeerTipClaimed(peer2, peer2Headers.Last());

            this.puller.RequestBlocksDownload(peer1Headers);

            await this.puller.AssignDownloadJobsAsync();

            this.puller.PushBlock(peer1Headers.First().HashBlock, this.helper.GenerateBlock(100), peer2.Connection.Id);

            foreach (ChainedHeader chainedHeader in this.puller.AssignedHeadersByPeerId[peer1.Connection.Id])
                Assert.Contains(chainedHeader, peer1Headers);

            Assert.Empty(this.helper.CallbacksCalled);
            Assert.Equal(peer1Headers.Count, this.puller.AssignedDownloadsByHash.Count);
        }

        private void VerifyAssignedDownloadsSortedOrder()
        {
            int previousHeight = -1;

            foreach (AssignedDownload assignedDownload in this.puller.AssignedDownloadsSorted)
            {
                if (previousHeight != -1)
                    Assert.True(assignedDownload.Header.Height >= previousHeight);

                previousHeight = assignedDownload.Header.Height;
            }
        }

        private readonly Random random = new Random();

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
