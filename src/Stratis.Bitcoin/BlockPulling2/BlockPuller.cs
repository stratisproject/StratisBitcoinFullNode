using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
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

        private OnBlockDownloadedCallback OnDownloadedCallback;

        private Queue<DownloadJob> reassignedJobsQueue;
        private Queue<DownloadJob> downloadJobsQueue;
        private Dictionary<uint256, AssignedDownload> AssignedDownloads;
        private Dictionary<int, PeerPerformanceCounter> PeerPerformanceByPeerId;

        private Dictionary<int, ChainedHeader> peersToTips;
        private Dictionary<int, BlockPullerBehavior> pullerBehaviors;

        private CancellationTokenSource cancellationSource;

        private CircularArray<long> BlockSizeSamples;
        public double AverageBlockSizeBytes { get; private set; }

        private AsyncManualResetEvent processQueuesSignal;
        private object lockObject;
        private int currentJobId;

        /// <summary>Amount of blocks that are being downloaded.</summary>
        private int pendingDownloadsCount;

        /// <summary>
        /// The maximum blocks that can be downloaded simountanously.
        /// Given that all peers are on the same chain they will deliver that amount of blocks in 1 seconds.
        /// </summary>
        private int maxBlocksBeingDownloaded;

        private readonly IInitialBlockDownloadState ibdState;
        private readonly ILogger logger;

        private Task assignerLoop;
        private Task stallingLoop;

        private int GetTotalSpeedOfAllPeersBytesPerSec()
        {
            return this.PeerPerformanceByPeerId.Sum(x => x.Value.SpeedBytesPerSecond);
        }

        public BlockPuller(OnBlockDownloadedCallback callback, IInitialBlockDownloadState ibdState, LoggerFactory loggerFactory)
        {
            this.peersToTips = new Dictionary<int, ChainedHeader>();
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.AssignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.BlockSizeSamples = new CircularArray<long>(1000);

            this.PeerPerformanceByPeerId = new Dictionary<int, PeerPerformanceCounter>();
            this.pullerBehaviors = new Dictionary<int, BlockPullerBehavior>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.lockObject = new object();
            this.currentJobId = 0;
            this.AverageBlockSizeBytes = 0;

            this.pendingDownloadsCount = 0;

            this.cancellationSource = new CancellationTokenSource();

            this.OnDownloadedCallback = callback;
            this.ibdState = ibdState;
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

                if (this.peersToTips.ContainsKey(peerId))
                {
                    this.peersToTips.AddOrReplace(peerId, newTip);
                }
                else
                {
                    this.peersToTips.AddOrReplace(peerId, newTip);
                    this.pullerBehaviors.Add(peerId, peer.Behavior<BlockPullerBehavior>());
                }
            }
        }

        public void PeerDisconnected(int peerId)
        {
            lock (this.lockObject)
            {
                this.peersToTips.Remove(peerId);
                this.pullerBehaviors.Remove(peerId);

                this.ReassignDownloads(peerId);
            }
        }

        // Accepts only consequtive headers (but gaps are ok: a1=a2=a3=a4=a8=a9)
        public void RequestBlocksDownload(List<ChainedHeader> headers, OnBlockDownloadedCallback callback)
        {
            var headersToEnqueue = new List<ChainedHeader>(headers.Count);

            lock (this.lockObject)
            {
                foreach (ChainedHeader header in headers)
                {
                    if (this.AssignedDownloads.TryGetValue(header.HashBlock, out AssignedDownload assignedDownload))
                    {
                        // Already assigned, just add one more callback.
                        assignedDownload.Callbacks.Add(callback);
                    }
                    else
                        headersToEnqueue.Add(header);
                }

                // Enqueue new download job.
                this.downloadJobsQueue.Enqueue(new DownloadJob()
                {
                    Hashes = headersToEnqueue.Select(x => x.HashBlock).ToList(),
                    Callbacks = new List<OnBlockDownloadedCallback>() { callback },
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

                
                this.AssignDownloadJobs();
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

                lock (this.lockObject)
                {
                    this.CheckStallingLocked();
                }
            }
        }

        // dequueues the downloadJobsQueue and reassignedDownloadJobsQueue
        private void AssignDownloadJobs()
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

                this.processQueuesSignal.Reset();
            }

            this.AskPeersForBlocks(newAssignments);

            // Call callbacks with null since puller failed to deliver requested blocks. //TODO this looks ugly
            foreach (DownloadJob failedJob in failedJobs)
            {
                foreach (uint256 failedHash in failedJob.Hashes)
                {
                    foreach (OnBlockDownloadedCallback callback in failedJob.Callbacks)
                    {
                        callback(failedHash, null);
                    }
                }
            }
        }

        // Ask peer behaviors to deliver blocks.
        private void AskPeersForBlocks(Dictionary<uint256, AssignedDownload> assignments)
        {
            // Form batches in order to ask for several blocks from one peer at once.
            foreach (IGrouping<int, KeyValuePair<uint256, AssignedDownload>> downloadsGroupedByPeerId in assignments.GroupBy(x => x.Value.PeerId))
            {
                var peerAssignments = new List<SingleAssignment>(downloadsGroupedByPeerId.Count());

                foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in downloadsGroupedByPeerId)
                {
                    peerAssignments.Add(new SingleAssignment()
                    {
                        Hash = assignedDownload.Key,
                        Callbacks = assignedDownload.Value.Callbacks,
                        JobId = assignedDownload.Value.JobId
                    });
                }

                try
                {
                    lock (this.lockObject)
                    {
                        //this.pullerBehaviors[currentPeerId].RequestBlocks(hashesToJobIds.Values); TODO
                    }
                }
                catch (Exception)
                {
                    // Failed to assign downloads to a peer. Put assignments back to the reassign queue and signal processing.
                    this.ReassignDownloads(peerAssignments);

                    this.PeerDisconnected(downloadsGroupedByPeerId.First().Value.PeerId);
                    this.processQueuesSignal.Reset();
                }
            }
        }

        // returns count of new assigned downloads to recalculate empty slots
        private Dictionary<uint256, AssignedDownload> DistributeHashesLocked(ref DownloadJob downloadJob, ref List<DownloadJob> failedJobs, int emptySlotes)
        {
            var newAssignments = new Dictionary<uint256, AssignedDownload>();
            var failedHashes = new List<uint256>();


            //TODO



            if (failedHashes.Count != 0)
            {
                failedJobs.Add(new DownloadJob()
                {
                    Hashes = failedHashes,
                    Callbacks = downloadJob.Callbacks
                });
            }

            return newAssignments;
        }

        private void CheckStallingLocked()
        {
            //TODO
        }

        // Callbacks from BlockPullerBehavior
        private void PushBlock(uint256 blockHash, Block block, int peerId)
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

                this.processQueuesSignal.Set();
            }

            foreach (OnBlockDownloadedCallback callback in assignedDownload.Callbacks)
                callback(blockHash, block);
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
            this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSec() / this.AverageBlockSizeBytes);

            if (this.maxBlocksBeingDownloaded < 10)
                this.maxBlocksBeingDownloaded = 10;
        }

        // finds all assigned blocks, removes from assigned downloads and adds to reassign queue
        private void ReassignDownloads(int peerId)
        {
            var assignments = new List<SingleAssignment>();

            lock (this.lockObject)
            {
                foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in this.AssignedDownloads.Where(x => x.Value.PeerId == peerId).ToList())
                {
                    assignments.Add(new SingleAssignment()
                    {
                        Hash = assignedDownload.Key,
                        Callbacks = assignedDownload.Value.Callbacks,
                        JobId = assignedDownload.Value.JobId
                    });

                    // Remove hash from assigned downloads.
                    this.AssignedDownloads.Remove(assignedDownload.Key);
                }
            }

            if (assignments.Count != 0)
                this.ReassignDownloads(assignments);
        }

        // puts hashesToJobId to reassign queue
        private void ReassignDownloads(List<SingleAssignment> assignments)
        {
            lock (this.lockObject)
            {
                foreach (IGrouping<int, SingleAssignment> jobGroup in assignments.GroupBy(x => x.JobId))
                {
                    // JobId and callbacks for jobs with same Id are the same
                    SingleAssignment firstAssignment = jobGroup.First();

                    var newJob = new DownloadJob()
                    {
                        Callbacks = firstAssignment.Callbacks,
                        Id = firstAssignment.JobId,
                        Hashes = jobGroup.Select(x => x.Hash).ToList()
                    };

                    this.reassignedJobsQueue.Enqueue(newJob);
                }
            }
        }

        public void Dispose()
        {
            this.cancellationSource.Cancel();

            this.assignerLoop?.GetAwaiter().GetResult();
            this.stallingLoop?.GetAwaiter().GetResult();

            this.cancellationSource.Dispose();
        }

        // ================================

        private struct SingleAssignment
        {
            public int JobId;

            public List<OnBlockDownloadedCallback> Callbacks;

            public uint256 Hash;
        }

        private struct DownloadJob
        {
            public int Id;

            public List<OnBlockDownloadedCallback> Callbacks;

            public List<uint256> Hashes;
        }

        private class AssignedDownload
        {
            public int JobId;

            public int PeerId;

            public DateTime AssignedTime;

            public List<OnBlockDownloadedCallback> Callbacks;

            public int BlockHeight;
        }
    }
}
