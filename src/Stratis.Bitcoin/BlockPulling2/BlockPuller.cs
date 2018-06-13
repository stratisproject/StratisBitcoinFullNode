using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    public class BlockPuller
    {
        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block, int peerId);

        private Dictionary<int, ChainedHeader> peersToTips;

        private Queue<DownloadJob> reassignedJobsQueue;
        private Queue<DownloadJob> downloadJobsQueue;

        private Dictionary<uint256, AssignedDownload> AssignedDownloads;

        private CircularArray<long> BlockSizeSamples;
        private Dictionary<int, PeerPerformanceSamples> PeerSamplesByPeerId;

        private ManualResetEventSlim processQueuesSignal;
        private object lockObject;
        private int currentJobId;

        private int pendingDownloadsCount;

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
            return this.PeerSamplesByPeerId.Sum(x => x.Value.GetSpeedBytesPerSecond());
        }

        public BlockPuller()
        {
            this.peersToTips = new Dictionary<int, ChainedHeader>();
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.AssignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.BlockSizeSamples = new CircularArray<long>(1000);

            this.PeerSamplesByPeerId = new Dictionary<int, PeerPerformanceSamples>();

            this.processQueuesSignal = new ManualResetEventSlim(false);
            this.lockObject = new object();
            this.currentJobId = 0;

            this.pendingDownloadsCount = 0;
        }

        public void Initialize()
        {

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
                this.AddPeerSampleLocked(peerId, block.BlockSize.Value, deliveredInSeconds);

                this.RecalculateQualityScoresLocked();

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.processQueuesSignal.Set();
            }

            foreach (OnBlockDownloadedCallback callback in assignedDownload.Callbacks)
                callback(blockHash, block, peerId);
        }

        private void AddPeerSampleLocked(int peerId, long blockSizeBytes, double delaySeconds)
        {
            PeerPerformanceSamples samples;

            if (!this.PeerSamplesByPeerId.TryGetValue(peerId, out samples))
            {
                samples = new PeerPerformanceSamples();
                this.PeerSamplesByPeerId.Add(peerId, samples);
            }

            samples.AddSample(blockSizeBytes, delaySeconds);
        }

        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSec() / this.GetAverageBlockSizeBytes());

            if (this.maxBlocksBeingDownloaded < 10)
                this.maxBlocksBeingDownloaded = 10;
        }

        private void RecalculateQualityScoresLocked()
        {
            //TODO recalculate quality score
            //recalculate quality score of the peer
            //when we start recalculating QS check if we are the best peer. if not - update our QS only, otherwise our and everyone's.
            //if we updated only ours - then check if we are the best peer again. if true - update everyone.


        }

        private void ReassignDownloadsLocked(int peerId)
        {

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

        private class PeerPerformanceSamples
        {
            public double QualityScore;

            public const double MinQualityScore = 0.01;
            public const double SamplelessQualityScore = 0.3;
            public const double MaxQualityScore = 1.0;

            private CircularArray<SizeDelaySample> blockSizeDelaySecondsSamples;

            public PeerPerformanceSamples()
            {
                this.blockSizeDelaySecondsSamples = new CircularArray<SizeDelaySample>(100);
                this.QualityScore = SamplelessQualityScore;
            }

            public void AddSample(long blockSizeBytes, double delaySeconds)
            {
                this.blockSizeDelaySecondsSamples.Add(new SizeDelaySample()
                {
                    SizeBytes = blockSizeBytes,
                    DelaySeconds = delaySeconds
                }, out SizeDelaySample unused);
            }

            public int GetSpeedBytesPerSecond()
            {
                double avgSize = this.blockSizeDelaySecondsSamples.Average(x => x.SizeBytes);
                double avgDelay = this.blockSizeDelaySecondsSamples.Average(x => x.DelaySeconds);

                double speed = avgSize / avgDelay;
                return (int)speed;
            }

            public struct SizeDelaySample
            {
                public long SizeBytes;
                public double DelaySeconds;
            }
        }
    }
}
