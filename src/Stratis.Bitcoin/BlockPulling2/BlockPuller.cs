using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    /// <summary>
    /// TODO write big nice comment
    /// </summary>
    public class BlockPuller : IDisposable
    {
        /// <summary>Interval between checking if peers that were assigned important blocks didn't deliver the block.</summary>
        private const int StallingLoopIntervalMs = 500;

        /// <summary>The minimum empty slots percentage to start processing <see cref="downloadJobsQueue"/>.</summary>
        private const int MinEmptySlotsPercentageToStartProcessingTheQueue = 10;

        /// <summary>
        /// Defines which blocks are considered to be important.
        /// If requested block height is less than out consensus tip height plus this value then the block is considered to be important.
        /// </summary>
        private const int ImportantHeightMargin = 10;

        /// <summary>The maximum time in seconds in which peer should deliver an assigned block.</summary>
        /// <remarks>If peer failes to deliver in that time his assignments will be released and the peer penalized.</remarks>
        private const int MaxSecondsToDeliverBlock = 5;

        /// <summary>This affects quality score only. If the peer is too fast don't give him all the assignments in the world when not in IBD.</summary>
        private const int PeerSpeedLimitWhenNotInIBDBytesPerSec = 1024 * 1024;

        /// <summary>Callback which is called when puller received a block which it was asked for.</summary>
        /// <param name="blockHash">Hash of the delivered block.</param>
        /// <param name="block">The block.</param>
        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block);

        private readonly OnBlockDownloadedCallback OnDownloadedCallback;

        /// <summary>Queue of download jobs which were released from the peers that failed to deliver in time or were disconnected.</summary>
        private readonly Queue<DownloadJob> reassignedJobsQueue;

        /// <summary>Queue of download jobs which should be assigned to peers.</summary>
        private readonly Queue<DownloadJob> downloadJobsQueue;

        /// <summary>Collection of all download assignments to the peers sorted by block height.</summary>
        private Dictionary<uint256, AssignedDownload> AssignedDownloads;

        /// <summary>Headers of requested blocks mapped by hash.</summary>
        private readonly Dictionary<uint256, ChainedHeader> HeadersByHash;

        /// <summary>Peer tips mapped by peers id.</summary>
        private readonly Dictionary<int, ChainedHeader> peerIdsToTips;

        /// <summary>Block puller behaviors mapped by peer id.</summary>
        private readonly Dictionary<int, BlockPullerBehavior> pullerBehaviorsByPeerId;

        private readonly CancellationTokenSource cancellationSource;
        
        private readonly AverageCalculator averageBlockSizeBytes;

        /// <summary>Signaler that triggers <see cref="reassignedJobsQueue"/> and <see cref="downloadJobsQueue"/> processing when set.</summary>
        private readonly AsyncManualResetEvent processQueuesSignal;

        /// <summary>Unique identifier which will be set to the next created download job.</summary>
        private int nextJobId;

        /// <summary>Locks access to <see cref="processQueuesSignal"/> and all collections in this class.</summary>
        private readonly object lockObject;

        /// <summary>Amount of blocks that are being downloaded.</summary>
        private int pendingDownloadsCount;

        /// <summary>
        /// The maximum blocks that can be downloaded simultaneously.
        /// Given that all peers are on the same chain they will deliver that amount of blocks in 1 seconds.
        /// </summary>
        private int maxBlocksBeingDownloaded;

        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        /// <inheritdoc cref="ChainState"/>
        private readonly ChainState chainState;

        /// <inheritdoc cref="NetworkPeerRequirement"/>
        private readonly NetworkPeerRequirement networkPeerRequirement;

        /// <inheritdoc cref="random"/>
        private readonly Random random;

        /// <summary>Loop that assigns download jobs to the peers.</summary>
        private Task assignerLoop;

        /// <summary>Loop that checks if peers failed to deliver important blocks in given time and penalizes them if they did.</summary>
        private Task stallingLoop;

        public BlockPuller(OnBlockDownloadedCallback callback, ChainState chainState, ProtocolVersion protocolVersion, LoggerFactory loggerFactory)
        {
            this.peerIdsToTips = new Dictionary<int, ChainedHeader>();
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.AssignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.averageBlockSizeBytes = new AverageCalculator(1000);
            
            this.pullerBehaviorsByPeerId = new Dictionary<int, BlockPullerBehavior>();
            this.HeadersByHash = new Dictionary<uint256, ChainedHeader>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.lockObject = new object();
            this.nextJobId = 0;

            this.pendingDownloadsCount = 0;

            this.networkPeerRequirement = new NetworkPeerRequirement
            {
                MinVersion = protocolVersion,
                RequiredServices = NetworkPeerServices.Network
            };

            this.cancellationSource = new CancellationTokenSource();
            this.random = new Random();

            this.OnDownloadedCallback = callback;
            this.chainState = chainState;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.logger.LogTrace("()");

            this.assignerLoop = this.AssignerLoopAsync();
            this.stallingLoop = this.StallingLoopAsync();

            this.logger.LogTrace("(-)");
        }

        public double GetAverageBlockSizeBytes()
        {
            return this.averageBlockSizeBytes.Average;
        }

        private int GetTotalSpeedOfAllPeersBytesPerSecLocked()
        {
            return this.pullerBehaviorsByPeerId.Sum(x => x.Value.SpeedBytesPerSecond);
        }

        /// <summary>Should be called when a peer claims a new tip.</summary>
        /// <param name="peer">The peer.</param>
        /// <param name="newTip">New tip.</param>
        public void NewPeerTipClaimed(INetworkPeer peer, ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(peer.Connection.Id), peer.Connection.Id, nameof(newTip), newTip);

            lock (this.lockObject)
            {
                int peerId = peer.Connection.Id;

                if (this.peerIdsToTips.ContainsKey(peerId))
                {
                    this.peerIdsToTips.AddOrReplace(peerId, newTip);

                    this.logger.LogDebug("Tip for peer with id {0} was changed to '{1}'.", peerId, newTip);
                }
                else
                {
                    bool supportsRequirments = this.networkPeerRequirement.Check(peer.PeerVersion);

                    if (supportsRequirments)
                    {
                        this.peerIdsToTips.AddOrReplace(peerId, newTip);
                        this.pullerBehaviorsByPeerId.Add(peerId, peer.Behavior<BlockPullerBehavior>());

                        this.logger.LogDebug("New peer with id {0} and tip '{1}' was added.", peerId, newTip);
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Should be called when peer is disconnected.</summary>
        /// <param name="peerId">Unique peer identifier.</param>
        public void PeerDisconnected(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            lock (this.lockObject)
            {
                this.peerIdsToTips.Remove(peerId);
                this.pullerBehaviorsByPeerId.Remove(peerId);
            }

            this.ReleaseAssignments(peerId);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Requests the blocks for download.</summary>
        /// <remarks>Doesn't support asking for the same hash twice before getting a response.</remarks>
        /// <param name="headers">Collection of consecutive headers (but gaps are ok: a1=a2=a3=a4=a8=a9).</param>
        public void RequestBlocksDownload(List<ChainedHeader> headers)
        {
            this.logger.LogTrace("({0}:{1})", nameof(headers.Count), headers.Count);

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
                    Id = this.nextJobId++
                });

                this.processQueuesSignal.Set();

                this.logger.LogDebug("{0} blocks were requested from puller.", headers.Count);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Loop that assigns download jobs to the peers.</summary>
        private async Task AssignerLoopAsync()
        {
            this.logger.LogTrace("()");

            while (!this.cancellationSource.IsCancellationRequested)
            {
                try
                {
                    await this.processQueuesSignal.WaitAsync(this.cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("(-)[CANCELLED]");
                    return;
                }

                await this.AssignDownloadJobsAsync().ConfigureAwait(false);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Loop that continuously checks if peers failed to deliver important blocks in given time and penalizes them if they did.</summary>
        private async Task StallingLoopAsync()
        {
            this.logger.LogTrace("()");

            while (!this.cancellationSource.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(StallingLoopIntervalMs, this.cancellationSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    this.logger.LogTrace("(-)[CANCELLED]");
                    return;
                }
                
                this.CheckStalling();
            }

            this.logger.LogTrace("(-)");
        }

        private async Task AssignDownloadJobsAsync()
        {
            this.logger.LogTrace("()");

            var failedJobs = new List<DownloadJob>();
            var newAssignments = new Dictionary<uint256, AssignedDownload>();

            lock (this.lockObject)
            {
                // First process reassign queue ignoring slots limitations.
                while (this.reassignedJobsQueue.Count > 0)
                {
                    DownloadJob jobToReassign = this.reassignedJobsQueue.Dequeue();
                    this.logger.LogDebug("Reassigning job {0} with {1} hashes.", jobToReassign.Id, jobToReassign.Hashes.Count);

                    Dictionary<uint256, AssignedDownload> assignments = this.DistributeHashesLocked(ref jobToReassign, ref failedJobs, int.MaxValue);

                    foreach (KeyValuePair<uint256, AssignedDownload> assignment in assignments)
                        newAssignments.Add(assignment.Key, assignment.Value);
                }

                // Process regular queue.
                int emptySlots = this.maxBlocksBeingDownloaded - this.pendingDownloadsCount;

                this.logger.LogDebug("There are {0} empty slots.", emptySlots);

                if (emptySlots > (this.maxBlocksBeingDownloaded / 100) * MinEmptySlotsPercentageToStartProcessingTheQueue)
                {
                    while (this.downloadJobsQueue.Count > 0 && emptySlots > 0)
                    {
                        DownloadJob jobToAassign = this.downloadJobsQueue.Peek();
                        this.logger.LogDebug("Reassigning job {0} with {1} hashes.", jobToAassign.Id, jobToAassign.Hashes.Count);

                        Dictionary<uint256, AssignedDownload> assignments = this.DistributeHashesLocked(ref jobToAassign, ref failedJobs, emptySlots);
                        emptySlots -= assignments.Count;
                        this.logger.LogDebug("Reassigned {0} hashes.", assignments.Count);

                        foreach (KeyValuePair<uint256, AssignedDownload> assignment in assignments)
                            newAssignments.Add(assignment.Key, assignment.Value);

                        // Remove job from the queue if it was fully consumed.
                        if (jobToAassign.Hashes.Count == 0)
                        {
                            this.downloadJobsQueue.Dequeue();
                            this.logger.LogDebug("Job {0} was fully assigned.", jobToAassign.Id);
                        }
                    }
                }

                // Add newly assigned downloads and sort all assignments by block height.
                this.AssignedDownloads = this.AssignedDownloads.Concat(newAssignments).OrderBy(x => x.Value.BlockHeight).ToDictionary(k => k.Key, v => v.Value);
                
                // Remove failed hashes from HeadersByHash
                foreach (DownloadJob failedJob in failedJobs)
                {
                    foreach (uint256 failedHash in failedJob.Hashes)
                        this.HeadersByHash.Remove(failedHash);
                }

                this.processQueuesSignal.Reset();

                this.logger.LogDebug("Total amount of downloads assigned in this iteration is {0}.", newAssignments.Count);
            }
            
            // Call callbacks with null since puller failed to deliver requested blocks.
            foreach (DownloadJob failedJob in failedJobs)
            {
                foreach (uint256 failedHash in failedJob.Hashes)
                    this.OnDownloadedCallback(failedHash, null);
            }

            if (newAssignments.Count != 0)
                await this.AskPeersForBlocksAsync(newAssignments).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }


        /// <summary>Asks peer behaviors in parallel to deliver blocks.</summary>
        /// <param name="assignments">Assignments given to peers.</param>
        private async Task AskPeersForBlocksAsync(Dictionary<uint256, AssignedDownload> assignments)
        {
            this.logger.LogTrace("({0}:{1})", nameof(assignments.Count), assignments.Count);

            int maxDegreeOfParallelism = 8;

            // Form batches in order to ask for several blocks from one peer at once.
            await assignments.GroupBy(x => x.Value.PeerId).ForEachAsync(maxDegreeOfParallelism, CancellationToken.None, async (downloadsGroupedByPeerId, cancellation) =>
            {
                List<uint256> hashes = downloadsGroupedByPeerId.Select(x => x.Key).ToList();
                int peerId = downloadsGroupedByPeerId.First().Value.PeerId;

                BlockPullerBehavior peerBehavior;

                lock (this.lockObject)
                {
                    peerBehavior = this.pullerBehaviorsByPeerId[peerId];
                }

                bool success = false;

                if (peerBehavior != null)
                {
                    try
                    {
                        await peerBehavior.RequestBlocksAsync(hashes).ConfigureAwait(false);
                        success = true;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                if (!success)
                {
                    this.logger.LogDebug("Failed to ask peer {0} for some blocks.", peerId);

                    // Failed to assign downloads to a peer. Put assignments back to the reassign queue and signal processing.
                    var jobIdToHash = new Dictionary<int, uint256>(downloadsGroupedByPeerId.Count());

                    foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in downloadsGroupedByPeerId)
                        jobIdToHash.Add(assignedDownload.Value.JobId, assignedDownload.Key);

                    this.ReleaseAssignments(jobIdToHash);

                    this.PeerDisconnected(downloadsGroupedByPeerId.First().Value.PeerId);
                    this.processQueuesSignal.Reset();
                }
            }).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Distributes hashes from a provided download jobs between peers that can provide blocks represented by those hashes.</summary>
        /// <remarks>If some of the blocks from the job can't be provided by any peer those hashes will be added to a <param name="failedJobs"> as a new item.</param></remarks>
        /// <param name="downloadJob">Download job to be partially of fully consumed.</param>
        /// <param name="failedJobs">Failed assignments.</param>
        /// <param name="emptySlotes">Amount of empty slots. This is the maximum amount of assignments that can be created.</param>
        /// <returns></returns>
        private Dictionary<uint256, AssignedDownload> DistributeHashesLocked(ref DownloadJob downloadJob, ref List<DownloadJob> failedJobs, int emptySlotes)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(downloadJob.Hashes.Count), downloadJob.Hashes.Count, nameof(failedJobs.Count), failedJobs.Count, nameof(emptySlotes), emptySlotes);

            var newAssignments = new Dictionary<uint256, AssignedDownload>();
            
            var peers = new Dictionary<int, ChainedHeader>(this.peerIdsToTips);
            bool jobFailed = false;

            foreach (uint256 hashToAssign in downloadJob.Hashes.Take(emptySlotes).ToList())
            {
                ChainedHeader header = this.HeadersByHash[hashToAssign];

                while (!jobFailed)
                {
                    double sumOfQualityScores = this.pullerBehaviorsByPeerId.Values.Sum(x => x.QualityScore);

                    double scoreToReachPeer = this.random.Next(0, (int)(sumOfQualityScores * 1000)) / 1000.0;

                    int peerId = 0;

                    foreach (BlockPullerBehavior pullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    {
                        if (pullerBehavior.QualityScore >= scoreToReachPeer)
                            peerId = pullerBehavior.AttachedPeer.Connection.Id;
                        else
                            scoreToReachPeer -= pullerBehavior.QualityScore;
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

                        this.logger.LogTrace("Block '{0}' was assigned to peer {1}.", hashToAssign, peerTip);

                        downloadJob.Hashes.Remove(hashToAssign);
                        break;
                    }
                    else
                    {
                        // Peer doesn't claim this hash.
                        peers.Remove(peerId);

                        if (peers.Count != 0)
                            continue;

                        jobFailed = true;

                        this.logger.LogDebug("Job {0} failed because there is no peer claiming {1} of it's hashes.", downloadJob.Id, downloadJob.Hashes.Count);
                    }
                }

                this.pendingDownloadsCount += newAssignments.Count;
            }

            if (jobFailed)
            {
                failedJobs.Add(new DownloadJob() { Hashes = downloadJob.Hashes });

                downloadJob.Hashes = new HashSet<uint256>();
            }

            this.logger.LogTrace("(-):*.{0}:{1}", nameof(newAssignments.Count), newAssignments.Count);
            return newAssignments;
        }

        /// <summary>Checks if peers failed to deliver important blocks and penalizes them if they did.</summary>
        private void CheckStalling()
        {
            this.logger.LogTrace("()");

            int lastImportantHeight = this.chainState.ConsensusTip.Height + ImportantHeightMargin;

            this.logger.LogTrace("Blocks up to height {0} are considered to be important.", lastImportantHeight);

            var toReassign = new Dictionary<int, uint256>();

            lock (this.lockObject)
            {
                bool reassigned = false;

                do
                {
                    foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in this.AssignedDownloads)
                    {
                        // Since the dictionary is sorted by height after we found first not important block we can assume that the rest of them are not important.
                        if (assignedDownload.Value.BlockHeight > lastImportantHeight)
                            break;

                        double secondsPassed = (DateTime.UtcNow - assignedDownload.Value.AssignedTime).TotalSeconds;

                        if (secondsPassed < MaxSecondsToDeliverBlock)
                            continue;

                        // Peer failed to deliver important block. Reassign all his jobs.
                        List<KeyValuePair<uint256, AssignedDownload>> downloadsToReassign = this.AssignedDownloads.Where(x => x.Value.PeerId == assignedDownload.Value.PeerId).ToList();

                        foreach (KeyValuePair<uint256, AssignedDownload> peerAssignment in downloadsToReassign)
                        {
                            this.AssignedDownloads.Remove(peerAssignment.Key);
                            toReassign.Add(peerAssignment.Value.JobId, peerAssignment.Key);
                        }

                        int reassignedCount = downloadsToReassign.Count;
                        int peerId = downloadsToReassign.First().Value.PeerId;

                        this.logger.LogDebug("Peer {0} failed to deliver {1} blocks from which some were important.", peerId, reassignedCount);

                        BlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];

                        pullerBehavior.Penalize(MaxSecondsToDeliverBlock, reassignedCount);

                        this.RecalculateQuealityScoreLocked(pullerBehavior, peerId);

                        reassigned = true;
                        
                        break; 
                    }
                } while (reassigned);
            }

            if (toReassign.Count != 0)
                this.ReleaseAssignments(toReassign);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Method which is called when <see cref="BlockPullerBehavior"/> receives a block.</summary>
        /// <param name="blockHash">The block hash.</param>
        /// <param name="block">The block.</param>
        /// <param name="peerId">Id of a peer that delivered a block.</param>
        public void PushBlock(uint256 blockHash, Block block, int peerId)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(blockHash), blockHash, nameof(peerId), peerId);

            AssignedDownload assignedDownload;

            lock (this.lockObject)
            {
                if (!this.AssignedDownloads.TryGetValue(blockHash, out assignedDownload))
                {
                    this.logger.LogTrace("(-)[WASNT_REQUESTED]");
                    return;
                }

                if (assignedDownload.PeerId != peerId)
                {
                    this.logger.LogTrace("(-)[WRONG_PEER_DELIVERED]");
                    return;
                }

                this.pendingDownloadsCount--;

                this.AssignedDownloads.Remove(blockHash);
                
                this.averageBlockSizeBytes.AddSample(block.BlockSize.Value);

                double deliveredInSeconds = (DateTime.UtcNow - assignedDownload.AssignedTime).TotalSeconds;

                this.logger.LogTrace("Peer {0} delivered block '{1}' in {2} seconds.", assignedDownload.PeerId, blockHash, deliveredInSeconds);
                
                // Add peer sample.
                BlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];
                pullerBehavior.AddSample(block.BlockSize.Value, deliveredInSeconds);

                // Recalculate quality score.
                this.RecalculateQuealityScoreLocked(pullerBehavior, peerId);

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.HeadersByHash.Remove(blockHash);

                this.processQueuesSignal.Set();
            }

            this.OnDownloadedCallback(blockHash, block);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Recalculates quality score of a peer or all peers if given peer has the best upload speed.</summary>
        /// <param name="pullerBehavior">The puller behavior of a peer which quality score should be recalculated.</param>
        /// <param name="peerId">Id of a peer which behavior is passed.</param>
        private void RecalculateQuealityScoreLocked(BlockPullerBehavior pullerBehavior, int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            // Now decide if we need to recalculate quality score for all peers or just for this one.
            int bestSpeed = this.pullerBehaviorsByPeerId.Max(x => x.Value.SpeedBytesPerSecond);
            int adjustedBestSpeed = bestSpeed > PeerSpeedLimitWhenNotInIBDBytesPerSec ? PeerSpeedLimitWhenNotInIBDBytesPerSec : bestSpeed;

            if (pullerBehavior.SpeedBytesPerSecond != bestSpeed)
            {
                // This is not the best peer. Recalculate it's score only.
                pullerBehavior.RecalculateQualityScore(adjustedBestSpeed);
            }
            else
            {
                this.logger.LogTrace("Peer {0} is the fastest peer. Recalculating quality score of all peers.", peerId);

                // This is the best peer. Recalculate quality score for everyone.
                foreach (BlockPullerBehavior peerPullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    peerPullerBehavior.RecalculateQualityScore(adjustedBestSpeed);
            }

            this.logger.LogTrace("(-)");
        }

        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            this.logger.LogTrace("()");

            // How many blocks we can download in 1 second.
            if (this.averageBlockSizeBytes.Average > 0)
                this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSecLocked() / this.averageBlockSizeBytes.Average);

            if (this.maxBlocksBeingDownloaded < 10)
                this.maxBlocksBeingDownloaded = 10;

            this.logger.LogTrace("Max amount of blocks that can be downloaded at the same time is set to {0}", this.maxBlocksBeingDownloaded);

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>
        /// Finds all blocks assigned to a given peer, removes assignments
        /// from <see cref="AssignedDownloads"/> and adds to <see cref="reassignedJobsQueue"/>.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        private void ReleaseAssignments(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            var hashesToJobIds = new Dictionary<int, uint256>();

            lock (this.lockObject)
            {
                foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in this.AssignedDownloads.Where(x => x.Value.PeerId == peerId).ToList())
                {
                    hashesToJobIds.Add(assignedDownload.Value.JobId, assignedDownload.Key);

                    // Remove hash from assigned downloads.
                    this.AssignedDownloads.Remove(assignedDownload.Key);
                }
            }

            if (hashesToJobIds.Count != 0)
                this.ReleaseAssignments(hashesToJobIds);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Adds items from <paramref name="hashesToJobIds"/> to the <see cref="reassignedJobsQueue"/>.</summary>
        /// <param name="hashesToJobIds">Block hashes mapped to job ids.</param>
        private void ReleaseAssignments(Dictionary<int, uint256> hashesToJobIds)
        {
            this.logger.LogTrace("({0}:{1})", nameof(hashesToJobIds), hashesToJobIds.Count);

            lock (this.lockObject)
            {
                foreach (IGrouping<uint256, KeyValuePair<int, uint256>> jobGroup in hashesToJobIds.GroupBy(x => x.Value))
                {
                    var newJob = new DownloadJob()
                    {
                        Id = jobGroup.First().Key,
                        Hashes = new HashSet<uint256>(jobGroup.Select(x => x.Value))
                    };

                    this.reassignedJobsQueue.Enqueue(newJob);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Logs statistics to the console.</summary>
        private void ShowStats()
        {
            this.logger.LogTrace("()");

            //TODO: do that when component is activated.

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

            this.logger.LogTrace("(-)");
        }
        
        /// <inheritdoc />
        public void Dispose()
        {
            this.logger.LogTrace("()");

            this.cancellationSource.Cancel();

            this.assignerLoop?.GetAwaiter().GetResult();
            this.stallingLoop?.GetAwaiter().GetResult();

            this.cancellationSource.Dispose();
            
            this.logger.LogTrace("(-)");
        }

        /// <summary>Represents consecutive collection of hashes that are to be downloaded.</summary>
        private struct DownloadJob
        {
            /// <summary>Unique identifier of this job.</summary>
            public int Id;

            /// <summary>Hashes of blocks that are to be downloaded.</summary>
            public HashSet<uint256> Hashes;
        }

        /// <summary>Represents a single download assignment to a peer.</summary>
        private struct AssignedDownload
        {
            /// <summary>Unique identifier of a job to which this assignment belongs.</summary>
            public int JobId;

            /// <summary>Id of a peer that was assigned to deliver a block.</summary>
            public int PeerId;
            
            /// <summary>Time when download was assigned to a peer.</summary>
            public DateTime AssignedTime;

            /// <summary>Height of a block associated with this assignment.</summary>
            public int BlockHeight;
        }
    }
}
