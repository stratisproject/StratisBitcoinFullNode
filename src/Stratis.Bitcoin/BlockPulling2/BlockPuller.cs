using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    public class BlockPuller : IDisposable
    {
        private const int StallingDelayMs = 500;
        private const int MinEmptySlotsPercentageToStartProcessingTheQueue = 5;
        
        /// <summary>This affects quality score only. If the peer is too fast don't give him all the assignments in the world when not in IBD.</summary>
        private const int PeerSpeedLimitLimirationWhenNotInIBDBytes = 1024 * 1024;

        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block);

        private readonly OnBlockDownloadedCallback OnDownloadedCallback;

        private readonly Queue<DownloadJob> reassignedJobsQueue;
        private readonly Queue<DownloadJob> downloadJobsQueue;
        private readonly Dictionary<uint256, AssignedDownload> AssignedDownloads;
        private readonly Dictionary<int, PeerPerformanceCounter> PeerPerformanceByPeerId;

        private readonly Dictionary<uint256, ChainedHeader> HeadersByHash;

        private readonly Dictionary<int, ChainedHeader> peerIdsToTips;
        private readonly Dictionary<int, BlockPullerBehavior> pullerBehaviors;

        private readonly CancellationTokenSource cancellationSource;

        private readonly CircularArray<long> BlockSizeSamples;
        public double AverageBlockSizeBytes { get; private set; }

        private readonly AsyncManualResetEvent processQueuesSignal;
        private readonly object lockObject;
        private int currentJobId;

        /// <summary>Amount of blocks that are being downloaded.</summary>
        private int pendingDownloadsCount;

        /// <summary>
        /// The maximum blocks that can be downloaded simountanously.
        /// Given that all peers are on the same chain they will deliver that amount of blocks in 1 seconds.
        /// </summary>
        private int maxBlocksBeingDownloaded;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly ChainState chainState;
        private readonly ILogger logger;
        private readonly NetworkPeerRequirement networkPeerRequirement;

        private Task assignerLoop;
        private Task stallingLoop;

        private Random random;

        private int GetTotalSpeedOfAllPeersBytesPerSecLocked()
        {
            return this.PeerPerformanceByPeerId.Sum(x => x.Value.SpeedBytesPerSecond);
        }

        public BlockPuller(OnBlockDownloadedCallback callback, IInitialBlockDownloadState ibdState, ChainState chainState, ProtocolVersion protocolVersion, LoggerFactory loggerFactory)
        {
            this.peerIdsToTips = new Dictionary<int, ChainedHeader>();
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.AssignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.BlockSizeSamples = new CircularArray<long>(1000);

            this.PeerPerformanceByPeerId = new Dictionary<int, PeerPerformanceCounter>();
            this.pullerBehaviors = new Dictionary<int, BlockPullerBehavior>();
            this.HeadersByHash = new Dictionary<uint256, ChainedHeader>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.lockObject = new object();
            this.currentJobId = 0;
            this.AverageBlockSizeBytes = 0;

            this.pendingDownloadsCount = 0;

            this.networkPeerRequirement = new NetworkPeerRequirement
            {
                MinVersion = protocolVersion,
                RequiredServices = NetworkPeerServices.Network
            };

            this.cancellationSource = new CancellationTokenSource();
            this.random = new Random();

            this.OnDownloadedCallback = callback;
            this.ibdState = ibdState;
            this.chainState = chainState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.assignerLoop = this.AssignerLoopAsync();
            this.stallingLoop = this.StallingLoopAsync();
        }

        public void NewPeerTipClaimed(INetworkPeer peer, ChainedHeader newTip)
        {
            lock (this.lockObject)
            {
                int peerId = peer.Connection.Id;

                if (this.peerIdsToTips.ContainsKey(peerId))
                {
                    this.peerIdsToTips.AddOrReplace(peerId, newTip);
                }
                else
                {
                    bool supportsRequirments = this.networkPeerRequirement.Check(peer.PeerVersion);

                    if (supportsRequirments)
                    {
                        this.peerIdsToTips.AddOrReplace(peerId, newTip);
                        this.pullerBehaviors.Add(peerId, peer.Behavior<BlockPullerBehavior>());
                    }
                }
            }
        }

        public void PeerDisconnected(int peerId)
        {
            lock (this.lockObject)
            {
                this.peerIdsToTips.Remove(peerId);
                this.pullerBehaviors.Remove(peerId);
                this.PeerPerformanceByPeerId.Remove(peerId);

                this.ReleaseAssignments(peerId);
            }
        }

        // Accepts only hashes of consequtive headers (but gaps are ok: a1=a2=a3=a4=a8=a9)
        // Doesn't support asking for the same hash twice before getting a response
        public void RequestBlocksDownload(List<ChainedHeader> headers)
        {
            lock (this.lockObject)
            {
                var hashes = new HashSet<uint256>();

                foreach (ChainedHeader header in headers)
                {
                    this.HeadersByHash.Add(header.HashBlock, header);
                    hashes.Add(header.HashBlock);
                }
                
                // Enqueue new download job.
                this.downloadJobsQueue.Enqueue(new DownloadJob()
                {
                    Hashes = hashes,
                    Id = this.currentJobId++
                });

                this.processQueuesSignal.Set();
            }
        }

        private async Task AssignerLoopAsync()
        {
            while (!this.cancellationSource.IsCancellationRequested)
            {
                try
                {
                    await this.processQueuesSignal.WaitAsync(this.cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                
                await this.AssignDownloadJobsAsync().ConfigureAwait(false);
            }
        }

        private async Task StallingLoopAsync()
        {
            while (!this.cancellationSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(StallingDelayMs, this.cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                
                this.CheckStalling();
            }
        }

        // dequueues the downloadJobsQueue and reassignedDownloadJobsQueue
        private async Task AssignDownloadJobsAsync()
        {
            var failedJobs = new List<DownloadJob>();
            var newAssignments = new Dictionary<uint256, AssignedDownload>();

            lock (this.lockObject)
            {
                // First process reassign queue ignoring slots limitations.
                while (this.reassignedJobsQueue.Count > 0)
                {
                    DownloadJob jobToReassign = this.reassignedJobsQueue.Dequeue();

                    Dictionary<uint256, AssignedDownload> assignments = this.DistributeHashesLocked(ref jobToReassign, ref failedJobs, int.MaxValue);

                    foreach (KeyValuePair<uint256, AssignedDownload> assignment in assignments)
                        newAssignments.Add(assignment.Key, assignment.Value);
                }

                // Process regular queue.
                int emptySlots = this.maxBlocksBeingDownloaded - this.pendingDownloadsCount;

                if (emptySlots > (this.maxBlocksBeingDownloaded / 100) * MinEmptySlotsPercentageToStartProcessingTheQueue)
                {
                    while (this.downloadJobsQueue.Count > 0 && emptySlots > 0)
                    {
                        DownloadJob jobToAassign = this.downloadJobsQueue.Peek();

                        Dictionary<uint256, AssignedDownload> assignments = this.DistributeHashesLocked(ref jobToAassign, ref failedJobs, emptySlots);
                        emptySlots -= assignments.Count;

                        foreach (KeyValuePair<uint256, AssignedDownload> assignment in assignments)
                            newAssignments.Add(assignment.Key, assignment.Value);

                        // Remove job from the queue if it was fully consumed.
                        if (jobToAassign.Hashes.Count == 0)
                            this.downloadJobsQueue.Dequeue();
                    }
                }

                foreach (KeyValuePair<uint256, AssignedDownload> assignment in newAssignments)
                    this.AssignedDownloads.Add(assignment.Key, assignment.Value);

                // Remove failed hashes from HeadersByHash
                foreach (DownloadJob failedJob in failedJobs)
                {
                    foreach (uint256 failedHash in failedJob.Hashes)
                        this.HeadersByHash.Remove(failedHash);
                }

                this.processQueuesSignal.Reset();
            }

            await this.AskPeersForBlocksAsync(newAssignments).ConfigureAwait(false);

            // Call callbacks with null since puller failed to deliver requested blocks.
            foreach (DownloadJob failedJob in failedJobs)
            {
                foreach (uint256 failedHash in failedJob.Hashes)
                    this.OnDownloadedCallback(failedHash, null);
            }
        }

        // Ask peer behaviors to deliver blocks.
        private async Task AskPeersForBlocksAsync(Dictionary<uint256, AssignedDownload> assignments)
        {
            // Form batches in order to ask for several blocks from one peer at once.
            foreach (IGrouping<int, KeyValuePair<uint256, AssignedDownload>> downloadsGroupedByPeerId in assignments.GroupBy(x => x.Value.PeerId))
            {
                List<uint256> hashes = downloadsGroupedByPeerId.Select(x => x.Key).ToList();
                int peerId = downloadsGroupedByPeerId.First().Value.PeerId;
                
                try
                {
                    await this.pullerBehaviors[peerId].RequestBlocksAsync(hashes).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Failed to assign downloads to a peer. Put assignments back to the reassign queue and signal processing.
                    var jobIdToHash = new Dictionary<int, uint256>(downloadsGroupedByPeerId.Count());

                    foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in downloadsGroupedByPeerId)
                        jobIdToHash.Add(assignedDownload.Value.JobId, assignedDownload.Key);
                    
                    this.ReleaseAssignments(jobIdToHash);

                    this.PeerDisconnected(downloadsGroupedByPeerId.First().Value.PeerId);
                    this.processQueuesSignal.Reset();
                }
            }
        }

        // returns count of new assigned downloads to recalculate empty slots
        private Dictionary<uint256, AssignedDownload> DistributeHashesLocked(ref DownloadJob downloadJob, ref List<DownloadJob> failedJobs, int emptySlotes)
        {
            var newAssignments = new Dictionary<uint256, AssignedDownload>();
            
            var peers = new Dictionary<int, ChainedHeader>(this.peerIdsToTips);
            bool jobFailed = false;

            foreach (uint256 hashToAssign in downloadJob.Hashes.Take(emptySlotes).ToList())
            {
                ChainedHeader header = this.HeadersByHash[hashToAssign];

                while (!jobFailed)
                {
                    double sumOfQualityScores = this.PeerPerformanceByPeerId.Values.Sum(x => x.QualityScore);

                    double scoreToReachPeer = this.random.Next(0, (int)(sumOfQualityScores * 1000)) / 1000.0;

                    int peerId = 0;

                    foreach (KeyValuePair<int, PeerPerformanceCounter> performanceCounter in this.PeerPerformanceByPeerId)
                    {
                        if (performanceCounter.Value.QualityScore >= scoreToReachPeer)
                            peerId = performanceCounter.Key;
                        else
                            scoreToReachPeer -= performanceCounter.Value.QualityScore;
                    }

                    ChainedHeader peerTip = peers[peerId];

                    if (peerTip.GetAncestor(header.Height) == header)
                    {
                        // Assign to this peer
                        newAssignments.Add(hashToAssign, new AssignedDownload()
                        {
                            PeerId = peerId,
                            JobId = downloadJob.Id,
                            AssignedTime = DateTime.UtcNow,
                            BlockHeight = header.Height
                        });

                        this.pendingDownloadsCount++;

                        downloadJob.Hashes.Remove(hashToAssign); // TODO remove from the start of the list? Maybe use hashset or smth
                        break;
                    }
                    else
                    {
                        // Peer doesn't claim this hash.
                        peers.Remove(peerId);

                        if (peers.Count != 0)
                            continue;

                        // JOB FAILED!
                        jobFailed = true;
                    }
                }
            }

            if (jobFailed)
            {
                failedJobs.Add(new DownloadJob() {Hashes = downloadJob.Hashes });

                downloadJob.Hashes = new HashSet<uint256>();
            }

            return newAssignments;
        }

        private void CheckStalling()
        {
            int lastImportantHeight = this.chainState.ConsensusTip.Height + 10; //TODO move 10 to constant

            int maxSecondsToDeliverBlock = 5; // TODO Move to constant

            var toReassign = new Dictionary<int, uint256>();

            lock (this.lockObject)
            {
                bool reassigned = false;

                do
                {
                    KeyValuePair<uint256, AssignedDownload> expiredImportantDownload = this.AssignedDownloads.FirstOrDefault(x =>
                        x.Value.BlockHeight <= lastImportantHeight && (DateTime.UtcNow - x.Value.AssignedTime).TotalSeconds >= maxSecondsToDeliverBlock);
                    
                    if (!expiredImportantDownload.Equals(default(KeyValuePair<uint256, AssignedDownload>)))
                    {
                        // Peer failed to deliver important block. Reassign all his jobs.
                        List<KeyValuePair<uint256, AssignedDownload>> downloadsToReassign = this.AssignedDownloads.Where(x => x.Value.PeerId == expiredImportantDownload.Value.PeerId).ToList();
                        
                        foreach (KeyValuePair<uint256, AssignedDownload> peerAssignment in downloadsToReassign)
                        {
                            this.AssignedDownloads.Remove(peerAssignment.Key);
                            toReassign.Add(peerAssignment.Value.JobId, peerAssignment.Key);
                        }

                        int reassignedCount = downloadsToReassign.Count;

                        int tenPercentOfSamples = 10; // 10% of MaxSamples. TODO: calculate it  //TODO Test it and find best samples

                        int penalizeTimes = (reassignedCount < tenPercentOfSamples) ? reassignedCount : tenPercentOfSamples;

                        for (int i = 0; i < penalizeTimes; ++i)
                            this.AddPeerSampleAndRecalculateQualityScoreLocked(downloadsToReassign.First().Value.PeerId, 0, maxSecondsToDeliverBlock);
                        
                        reassigned = true;
                    }

                } while (reassigned);
            }

            this.ReleaseAssignments(toReassign);
        }

        // Callbacks from BlockPullerBehavior
        public void PushBlock(uint256 blockHash, Block block, int peerId)
        {
            AssignedDownload assignedDownload;

            lock (this.lockObject)
            {
                if (!this.AssignedDownloads.TryGetValue(blockHash, out assignedDownload))
                    return;

                if (assignedDownload.PeerId != peerId)
                    return;

                this.pendingDownloadsCount--;

                this.AssignedDownloads.Remove(blockHash);
                
                this.BlockSizeSamples.Add(block.BlockSize.Value, out long oldSample);
                this.AverageBlockSizeBytes = CircularArray<double>.RecalculateAverageForSircularArray(this.BlockSizeSamples.Count, this.AverageBlockSizeBytes, block.BlockSize.Value, oldSample);

                double deliveredInSeconds = (DateTime.UtcNow - assignedDownload.AssignedTime).TotalSeconds;
                this.AddPeerSampleAndRecalculateQualityScoreLocked(peerId, block.BlockSize.Value, deliveredInSeconds);

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.HeadersByHash.Remove(blockHash);

                this.processQueuesSignal.Set();
            }

            this.OnDownloadedCallback(blockHash, block);
        }

        private void AddPeerSampleAndRecalculateQualityScoreLocked(int peerId, long blockSizeBytes, double delaySeconds)
        {
            PeerPerformanceCounter performanceCounter;

            if (!this.PeerPerformanceByPeerId.TryGetValue(peerId, out performanceCounter))
            {
                performanceCounter = new PeerPerformanceCounter();
                this.PeerPerformanceByPeerId.Add(peerId, performanceCounter);
            }

            performanceCounter.AddSample(blockSizeBytes, delaySeconds);

            // Now decide if we need to recalculate quality score for all peers or just for this one.
            int bestSpeed = this.PeerPerformanceByPeerId.Max(x => x.Value.SpeedBytesPerSecond);
            int adjustedBestSpeed = bestSpeed > PeerSpeedLimitLimirationWhenNotInIBDBytes ? PeerSpeedLimitLimirationWhenNotInIBDBytes : bestSpeed;

            if (performanceCounter.SpeedBytesPerSecond != bestSpeed)
            {
                // This is not the best peer. Recalculate it's score only.
                performanceCounter.RecalculateQualityScore(adjustedBestSpeed);
            }
            else
            {
                // This is the best peer. Recalculate quality score for everyone.
                foreach (PeerPerformanceCounter peerPerformanceCounter in this.PeerPerformanceByPeerId.Values)
                    peerPerformanceCounter.RecalculateQualityScore(adjustedBestSpeed);
            }
        }

        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSecLocked() / this.AverageBlockSizeBytes);

            if (this.maxBlocksBeingDownloaded < 10)
                this.maxBlocksBeingDownloaded = 10;
        }

        // finds all assigned blocks, removes from assigned downloads and adds to reassign queue
        private void ReleaseAssignments(int peerId)
        {
            var jobIdToHash = new Dictionary<int, uint256>();

            lock (this.lockObject)
            {
                foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in this.AssignedDownloads.Where(x => x.Value.PeerId == peerId).ToList())
                {
                    jobIdToHash.Add(assignedDownload.Value.JobId, assignedDownload.Key);

                    // Remove hash from assigned downloads.
                    this.AssignedDownloads.Remove(assignedDownload.Key);
                }
            }

            if (jobIdToHash.Count != 0)
                this.ReleaseAssignments(jobIdToHash);
        }

        // puts hashesToJobId to reassign queue
        private void ReleaseAssignments(Dictionary<int, uint256> jobIdToHash)
        {
            lock (this.lockObject)
            {
                foreach (IGrouping<uint256, KeyValuePair<int, uint256>> jobGroup in jobIdToHash.GroupBy(x => x.Value))
                {
                    var newJob = new DownloadJob()
                    {
                        Id = jobGroup.First().Key,
                        Hashes = new HashSet<uint256>(jobGroup.Select(x => x.Value))
                    };

                    this.reassignedJobsQueue.Enqueue(newJob);
                }
            }
        }

        // Logs to console
        private void ShowStats()
        {
            //TODO 

            /*
             just for logging, only IBD)
	            show: // Show it as a part of nodestats, not separated spammer
		            avg download speed 
		            peer quality score (sort by quality score) 
		            number of assigned blocks
		            MaxBlocksBeingDownloaded
		            amount of blocks being downloaded
		            show actual speed (no 1mb limit)
             */
        }

        public void Dispose()
        {
            this.cancellationSource.Cancel();

            this.assignerLoop?.GetAwaiter().GetResult();
            this.stallingLoop?.GetAwaiter().GetResult();

            this.cancellationSource.Dispose();
        }
        
        private struct DownloadJob
        {
            public int Id;

            public HashSet<uint256> Hashes;
        }

        private struct AssignedDownload
        {
            public int JobId;

            public int PeerId;

            public DateTime AssignedTime;

            public int BlockHeight;
        }
    }
}
