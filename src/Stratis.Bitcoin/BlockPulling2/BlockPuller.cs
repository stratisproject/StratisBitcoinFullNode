using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    public class BlockPuller : IDisposable
    {
        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block, int peerId);

        private Dictionary<int, ChainedHeader> peersToTips;

        private Queue<DownloadJob> reassignedJobsQueue;
        private Queue<DownloadJob> downloadJobsQueue;

        private Dictionary<uint256, AssignedDownload> AssignedDownloads;

        private CircularArray<long> BlockSizeSamples;
        private Dictionary<int, PeerPerformanceCounter> PeerPerformanceByPeerId;

        private AsyncManualResetEvent processQueuesSignal;
        private object lockObject;
        private int currentJobId;

        private int pendingDownloadsCount;

        private Task assignerLoop;
        private Task stallingLoop;

        private CancellationTokenSource cancellationSource;

        private const int StallingDelayMs = 500;

        /// <summary>
        /// The maximum blocks that can be downloaded simountanously.
        /// Given that all peers are on the same chain they will deliver that amount of blocks in 1 seconds.
        /// </summary>
        private int maxBlocksBeingDownloaded;

        private long GetAverageBlockSizeBytes()
        {
            return (long)this.BlockSizeSamples.Average(x => x);
        }

        private int GetTotalSpeedOfAllPeersBytesPerSec()
        {
            return this.PeerPerformanceByPeerId.Sum(x => x.Value.SpeedBytesPerSecond);
        }

        public BlockPuller()
        {
            this.peersToTips = new Dictionary<int, ChainedHeader>();
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.AssignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.BlockSizeSamples = new CircularArray<long>(1000);

            this.PeerPerformanceByPeerId = new Dictionary<int, PeerPerformanceCounter>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.lockObject = new object();
            this.currentJobId = 0;

            this.pendingDownloadsCount = 0;

            this.cancellationSource = new CancellationTokenSource();
        }

        public void Initialize()
        {
            this.assignerLoop = this.AssignerLoopAsync();
            this.stallingLoop = this.StallingLoopAsync();
        }

        public void NewPeerTipClaimed(int peerId, ChainedHeader tip)
        {
            lock (this.lockObject)
            {
                this.peersToTips.AddOrReplace(peerId, tip);
            }
        }

        public void PeerDisconnected(int peerId)
        {
            lock (this.lockObject)
            {
                this.peersToTips.Remove(peerId);
                this.ReassignDownloadsLocked(peerId);
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
                    Callback = callback,
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

                lock (this.lockObject)
                {
                    this.AssignDownloadJobsLocked();
                }
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
        private void AssignDownloadJobsLocked()
        {
            // First process reassign queue ignoring slots limitations.
            while (this.reassignedJobsQueue.Count > 0)
            {
                DownloadJob jobToReassign = this.reassignedJobsQueue.Dequeue();

                Dictionary<uint256, AssignedDownload> newAssignments = this.DistributeHashesLocked(ref jobToReassign, out List<uint256> failedHashes, int.MaxValue);

                foreach (KeyValuePair<uint256, AssignedDownload> assignment in newAssignments)
                    this.AssignedDownloads.Add(assignment.Key, assignment.Value);
            }
            
            int emptySlots = this.maxBlocksBeingDownloaded - this.pendingDownloadsCount;

            //TODO now process normal QUEUE
        }

        private Dictionary<uint256, AssignedDownload> DistributeHashesLocked(ref DownloadJob downloadJob, out List<uint256> failedHashes, int emptySlotes)
        {
            throw new NotImplementedException();
        }

        private void CheckStallingLocked()
        {

        }

        // Callback from BlockPullerBehavior
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

                double deliveredInSeconds = (DateTime.UtcNow - assignedDownload.AssignedTime).TotalSeconds;
                this.AddPeerSampleAndRecalculateQualityScoreLocked(peerId, block.BlockSize.Value, deliveredInSeconds);

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.processQueuesSignal.Set();
            }

            foreach (OnBlockDownloadedCallback callback in assignedDownload.Callbacks)
                callback(blockHash, block, peerId);
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

            if (performanceCounter.SpeedBytesPerSecond != bestSpeed)
            {
                // This is not the best peer. Recalculate it's score only.
                performanceCounter.RecalculateQualityScore(bestSpeed);
            }
            else
            {
                // This is the best peer. Recalculate quality score for everyone.
                foreach (PeerPerformanceCounter peerPerformanceCounter in this.PeerPerformanceByPeerId.Values)
                    peerPerformanceCounter.RecalculateQualityScore(bestSpeed);
            }
        }

        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSec() / this.GetAverageBlockSizeBytes());

            if (this.maxBlocksBeingDownloaded < 10)
                this.maxBlocksBeingDownloaded = 10;
        }

        private void ReassignDownloadsLocked(int peerId)
        {

        }

        public void Dispose()
        {
            this.cancellationSource.Cancel();

            this.assignerLoop?.GetAwaiter().GetResult();
            this.stallingLoop?.GetAwaiter().GetResult();

            this.cancellationSource.Dispose();
        }

        // ================================

        private class DownloadJob
        {
            public int Id;

            public OnBlockDownloadedCallback Callback;

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
