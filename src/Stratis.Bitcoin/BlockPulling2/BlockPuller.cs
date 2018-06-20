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
    /// <summary>
    /// Thread-safe block puller which allows downloading blocks from all chains that the node is aware of.
    /// <remarks>
    /// It implements relative quality scoring for peers that are used for delivering requested blocks.
    /// <para>
    /// If peer that was assigned an important download fails to deliver in maximum allowed time, all his assignments will be reassigned.
    /// Reassigned downloads are processed with high priority comparing to regular requests.
    /// Blocks that are close to the node's consensus tip or behind it are considered to be important.
    /// </para>
    /// <para>
    /// Maximum amount of blocks that can be simultaneously downloaded depends on total speed of all peers that are capable of delivering blocks.
    /// </para>
    /// </remarks>
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
        /// <remarks>If peer fails to deliver in that time his assignments will be released and the peer penalized.</remarks>
        private const int MaxSecondsToDeliverBlock = 5;

        /// <summary>This affects quality score only. If the peer is too fast don't give him all the assignments in the world when not in IBD.</summary>
        private const int PeerSpeedLimitWhenNotInIBDBytesPerSec = 1024 * 1024;

        /// <summary>Callback which is called when puller received a block which it was asked for.</summary>
        /// <param name="blockHash">Hash of the delivered block.</param>
        /// <param name="block">The block.</param>
        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block);

        private readonly OnBlockDownloadedCallback OnDownloadedCallback;

        /// <summary>Queue of download jobs which were released from the peers that failed to deliver in time or were disconnected.</summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private readonly Queue<DownloadJob> reassignedJobsQueue;

        /// <summary>Queue of download jobs which should be assigned to peers.</summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private readonly Queue<DownloadJob> downloadJobsQueue;

        /// <summary>Collection of all download assignments to the peers sorted by block height.</summary>
        /// <remarks>Should be protected by <see cref="assignedLock"/>.</remarks>
        private readonly Dictionary<uint256, AssignedDownload> assignedDownloads;

        /// <summary>Assigned downloads sorted by block height.</summary>
        /// <remarks>Should be protected by <see cref="assignedLock"/>.</remarks>
        private readonly LinkedList<AssignedDownload> assignedDownloadsByHeights;

        /// <summary>Assigned hashes mapped by peer Id.</summary>
        /// <remarks>Should be protected by <see cref="assignedLock"/>.</remarks>
        private readonly Dictionary<int, HashSet<uint256>> assignedHashesByPeerId;

        /// <summary>Headers of requested blocks mapped by hash.</summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private readonly Dictionary<uint256, ChainedHeader> headersByHash;

        /// <summary>Block puller behaviors mapped by peer id.</summary>
        /// <remarks>Should be protected by <see cref="peerLock"/>.</remarks>
        private readonly Dictionary<int, BlockPullerBehavior> pullerBehaviorsByPeerId;

        private readonly CancellationTokenSource cancellationSource;
        
        /// <remarks>Should be protected by<see cref="queueLock"/>.</remarks>
        private readonly AverageCalculator averageBlockSizeBytes;

        /// <summary>Signaler that triggers <see cref="reassignedJobsQueue"/> and <see cref="downloadJobsQueue"/> processing when set.</summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private readonly AsyncManualResetEvent processQueuesSignal;

        /// <summary>Unique identifier which will be set to the next created download job.</summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private int nextJobId;

        /// <summary>Locks access to <see cref="pullerBehaviorsByPeerId"/>.</summary>
        private readonly object peerLock;

        /// <summary>
        /// Locks access to <see cref="headersByHash"/>, <see cref="processQueuesSignal"/>, <see cref="downloadJobsQueue"/>,
        /// <see cref="reassignedJobsQueue"/>, <see cref="maxBlocksBeingDownloaded"/>, <see cref="pendingDownloadsCount"/>,
        /// <see cref="nextJobId"/>, <see cref="averageBlockSizeBytes"/>.
        /// </summary>
        private readonly object queueLock;

        /// <summary>Locks access to <see cref="assignedDownloads"/>, <see cref="assignedHashesByPeerId"/>, <see cref="assignedDownloadsByHeights"/>.</summary>
        private readonly object assignedLock;

        /// <summary>Amount of blocks that are being downloaded.</summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private int pendingDownloadsCount;
        
        /// <summary>
        /// The maximum blocks that can be downloaded simultaneously.
        /// Given that all peers are on the same chain they will deliver that amount of blocks in 1 seconds.
        /// </summary>
        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private int maxBlocksBeingDownloaded;

        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        /// <inheritdoc cref="ChainState"/>
        private readonly ChainState chainState;

        /// <inheritdoc cref="NetworkPeerRequirement"/>
        private readonly NetworkPeerRequirement networkPeerRequirement;

        /// <inheritdoc cref="IInitialBlockDownloadState"/>
        private readonly IInitialBlockDownloadState ibdState;

        /// <inheritdoc cref="random"/>
        private readonly Random random;

        /// <summary>Loop that assigns download jobs to the peers.</summary>
        private Task assignerLoop;

        /// <summary>Loop that checks if peers failed to deliver important blocks in given time and penalizes them if they did.</summary>
        private Task stallingLoop;

        public BlockPuller(OnBlockDownloadedCallback callback, ChainState chainState, IInitialBlockDownloadState ibdState, ProtocolVersion protocolVersion, LoggerFactory loggerFactory)
        {
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.assignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.assignedDownloadsByHeights = new LinkedList<AssignedDownload>();
            this.assignedHashesByPeerId = new Dictionary<int, HashSet<uint256>>();

            this.averageBlockSizeBytes = new AverageCalculator(1000);
            
            this.pullerBehaviorsByPeerId = new Dictionary<int, BlockPullerBehavior>();
            this.headersByHash = new Dictionary<uint256, ChainedHeader>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.queueLock = new object();
            this.peerLock = new object();
            this.assignedLock = new object();
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
            this.ibdState = ibdState;
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
            lock (this.queueLock)
            {
                return this.averageBlockSizeBytes.Average;
            }
        }

        private int GetTotalSpeedOfAllPeersBytesPerSec()
        {
            lock (this.peerLock)
            {
                return this.pullerBehaviorsByPeerId.Sum(x => x.Value.SpeedBytesPerSecond);
            }
        }

        /// <summary>Should be called when IBD state was changed or first calculated.</summary>
        public void OnIbdStateChanged(bool isIbd)
        {
            lock (this.peerLock)
            {
                foreach (BlockPullerBehavior blockPullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    blockPullerBehavior.OnIbdStateChanged(isIbd);
            }
        }

        /// <summary>Should be called when a peer claims a new tip.</summary>
        /// <param name="peer">The peer.</param>
        /// <param name="newTip">New tip.</param>
        public void NewPeerTipClaimed(INetworkPeer peer, ChainedHeader newTip)
        {
            this.logger.LogTrace("({0}:{1},{2}:'{3}')", nameof(peer.Connection.Id), peer.Connection.Id, nameof(newTip), newTip);

            lock (this.peerLock)
            {
                int peerId = peer.Connection.Id;

                if (this.pullerBehaviorsByPeerId.ContainsKey(peerId))
                {
                    this.pullerBehaviorsByPeerId[peerId].Tip = newTip;
                    this.logger.LogTrace("Tip for peer with id {0} was changed to '{1}'.", peerId, newTip);
                }
                else
                {
                    bool supportsRequirments = this.networkPeerRequirement.Check(peer.PeerVersion);

                    if (supportsRequirments)
                    {
                        var behavior = peer.Behavior<BlockPullerBehavior>();
                        behavior.Tip = newTip;
                        this.pullerBehaviorsByPeerId.Add(peerId, behavior);

                        this.logger.LogDebug("New peer with id {0} and tip '{1}' was added.", peerId, newTip);
                    }
                    else
                        this.logger.LogTrace("Peer {0} was discarded since he doesn't support the requirements.", peerId);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Should be called when peer is disconnected.</summary>
        /// <param name="peerId">Unique peer identifier.</param>
        public void PeerDisconnected(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            lock (this.peerLock)
            {
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

            lock (this.queueLock)
            {
                var hashes = new List<uint256>();

                foreach (ChainedHeader header in headers)
                {
                    this.headersByHash.Add(header.HashBlock, header);
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

            lock (this.queueLock)
            {
                // First process reassign queue ignoring slots limitations.
                while (this.reassignedJobsQueue.Count > 0)
                {
                    DownloadJob jobToReassign = this.reassignedJobsQueue.Dequeue();
                    this.logger.LogDebug("Reassigning job {0} with {1} hashes.", jobToReassign.Id, jobToReassign.Hashes.Count);

                    Dictionary<uint256, AssignedDownload> assignments;

                    lock (this.peerLock)
                    {
                        assignments = this.DistributeHashesLocked(ref jobToReassign, ref failedJobs, int.MaxValue);
                    }

                    lock (this.assignedLock)
                    {
                        foreach (KeyValuePair<uint256, AssignedDownload> assignment in assignments)
                        {
                            newAssignments.Add(assignment.Key, assignment.Value);
                            this.AddAssignedDownloadLocked(assignment.Key, assignment.Value);
                        }
                    }
                }

                // Process regular queue.
                int emptySlots = this.maxBlocksBeingDownloaded - this.pendingDownloadsCount;

                this.logger.LogTrace("There are {0} empty slots.", emptySlots);

                if (emptySlots > this.maxBlocksBeingDownloaded / 100.0 * MinEmptySlotsPercentageToStartProcessingTheQueue)
                {
                    while ((this.downloadJobsQueue.Count > 0) && (emptySlots > 0))
                    {
                        DownloadJob jobToAassign = this.downloadJobsQueue.Peek();
                        int jobHashesCount = jobToAassign.Hashes.Count;

                        Dictionary<uint256, AssignedDownload> assignments;

                        lock (this.peerLock)
                        {
                            assignments = this.DistributeHashesLocked(ref jobToAassign, ref failedJobs, emptySlots);
                        }

                        emptySlots -= assignments.Count;
                        this.logger.LogDebug("Assigned {0} hashes out of {1} for job {2}", assignments.Count, jobHashesCount, jobToAassign.Id);

                        lock (this.assignedLock)
                        {
                            foreach (KeyValuePair<uint256, AssignedDownload> assignment in assignments)
                            {
                                newAssignments.Add(assignment.Key, assignment.Value);
                                this.AddAssignedDownloadLocked(assignment.Key, assignment.Value);
                            }
                        }

                        // Remove job from the queue if it was fully consumed.
                        if (jobToAassign.Hashes.Count == 0)
                            this.downloadJobsQueue.Dequeue();
                    }
                }
                
                // Remove failed hashes from headersByHash
                foreach (DownloadJob failedJob in failedJobs)
                {
                    foreach (uint256 failedHash in failedJob.Hashes)
                        this.headersByHash.Remove(failedHash);
                }

                this.processQueuesSignal.Reset();

                this.logger.LogTrace("Total amount of downloads assigned in this iteration is {0}.", newAssignments.Count);
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

        /// <summary>
        /// Adds assigned download to <see cref="assignedDownloads"/> and helper structures <see cref="assignedDownloadsByHeights"/> and <see cref="assignedHashesByPeerId"/>.
        /// </summary>
        /// <remarks>Should be locked by <see cref="assignedLock"/>.</remarks>
        /// <param name="hash">Hash of a block that is associated with <paramref name="assignment"/>.</param>
        /// <param name="assignment">The assignment.</param>
        private void AddAssignedDownloadLocked(uint256 hash, AssignedDownload assignment)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(hash), hash, nameof(assignment), assignment);

            this.assignedDownloads.Add(hash, assignment);

            // Add to assignedHashesByPeerId
            if (!this.assignedHashesByPeerId.TryGetValue(assignment.PeerId, out HashSet<uint256> hashesForIds))
            {
                hashesForIds = new HashSet<uint256>();
                this.assignedHashesByPeerId.Add(assignment.PeerId, hashesForIds);
            }
            hashesForIds.Add(hash);

            // Add to assignedDownloadsByHeights
            LinkedListNode<AssignedDownload> lastDownload = this.assignedDownloadsByHeights.Last;

            if ((lastDownload == null) || (lastDownload.Value.BlockHeight <= assignment.BlockHeight))
            {
                this.assignedDownloadsByHeights.AddLast(assignment);
            }
            else
            {
                LinkedListNode<AssignedDownload> current = lastDownload;

                while (true)
                {
                    current = current.Previous;

                    if (current.Value.BlockHeight <= assignment.BlockHeight)
                    {
                        this.assignedDownloadsByHeights.AddAfter(current, assignment);
                        break;
                    }
                }
            }
            
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Removed assigned download from <see cref="assignedDownloads"/> and helper structures <see cref="assignedDownloadsByHeights"/> and <see cref="assignedHashesByPeerId"/>.
        /// </summary>
        /// <remarks>Should be locked by <see cref="assignedLock"/>.</remarks>
        /// <param name="hash">Hash of a block that is associated with <paramref name="assignment"/>.</param>
        /// <param name="assignment">The assignment.</param>
        private bool TryRemoveAssignedDownloadLocked(uint256 hash, out AssignedDownload assignment)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(hash), hash);

            bool exists = this.assignedDownloads.TryGetValue(hash, out assignment);

            if (exists)
            {
                this.assignedDownloads.Remove(hash);

                if (this.assignedHashesByPeerId.TryGetValue(assignment.PeerId, out HashSet<uint256> hashesForIds))
                {
                    hashesForIds.Remove(hash);
                    if (hashesForIds.Count == 0)
                        this.assignedHashesByPeerId.Remove(assignment.PeerId);
                }

                LinkedListNode<AssignedDownload> current = this.assignedDownloadsByHeights.First;
                while (true)
                {
                    if (current.Value.BlockHash == assignment.BlockHash)
                    {
                        this.assignedDownloadsByHeights.Remove(current);
                        break;
                    }

                    current = current.Next;
                }
            }

            this.logger.LogTrace("(-):{0}", exists);
            return exists;
        }
        
        /// <summary>Asks peer behaviors in parallel to deliver blocks.</summary>
        /// <param name="assignments">Assignments given to peers.</param>
        private async Task AskPeersForBlocksAsync(Dictionary<uint256, AssignedDownload> assignments)
        {
            this.logger.LogTrace("({0}:{1})", nameof(assignments.Count), assignments.Count);

            int maxDegreeOfParallelism = 8;

            // Form batches in order to ask for several blocks from one peer at once.
            var hashesToPeerId = new Dictionary<int, List<uint256>>();
            foreach (KeyValuePair<uint256, AssignedDownload> assignedDownload in assignments)
            {
                if (!hashesToPeerId.TryGetValue(assignedDownload.Value.PeerId, out List<uint256> hashes))
                {
                    hashes = new List<uint256>();
                    hashesToPeerId.Add(assignedDownload.Value.PeerId, hashes);
                }

                hashes.Add(assignedDownload.Key);
            }
            
            await hashesToPeerId.ForEachAsync(maxDegreeOfParallelism, CancellationToken.None, async (peerIdToHashes, cancellation) =>
            {
                List<uint256> hashes = peerIdToHashes.Value;
                int peerId = peerIdToHashes.Key;

                BlockPullerBehavior peerBehavior;

                lock (this.peerLock)
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
                    this.logger.LogDebug("Failed to ask peer {0} for {1} blocks.", peerId, hashes.Count);

                    // Failed to assign downloads to a peer. Put assignments back to the reassign queue and signal processing.
                    var hashesToJobId = new Dictionary<uint256, int>(hashes.Count);

                    foreach (uint256 hashToReassign in hashes)
                        hashesToJobId.Add(hashToReassign, assignments[hashToReassign].JobId);

                    this.ReleaseAssignments(hashesToJobId);

                    this.PeerDisconnected(peerId);
                    this.processQueuesSignal.Reset();
                }
            }).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Distributes hashes from a provided download jobs between peers that can provide blocks represented by those hashes.</summary>
        /// <remarks>
        /// If some of the blocks from the job can't be provided by any peer those hashes will be added to a <param name="failedJobs"> as a new item.</param>
        /// <para>
        /// Should be locked by <see cref="peerLock"/> and <see cref="queueLock"/>.
        /// </para>
        /// </remarks>
        /// <param name="downloadJob">Download job to be partially of fully consumed.</param>
        /// <param name="failedJobs">Failed assignments.</param>
        /// <param name="emptySlotes">Amount of empty slots. This is the maximum amount of assignments that can be created.</param>
        private Dictionary<uint256, AssignedDownload> DistributeHashesLocked(ref DownloadJob downloadJob, ref List<DownloadJob> failedJobs, int emptySlotes)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(downloadJob.Hashes.Count), downloadJob.Hashes.Count, nameof(failedJobs.Count), failedJobs.Count, nameof(emptySlotes), emptySlotes);

            var newAssignments = new Dictionary<uint256, AssignedDownload>();
            
            var peerIdsToTips = new Dictionary<int, ChainedHeader>(this.pullerBehaviorsByPeerId.Count);
            foreach (KeyValuePair<int, BlockPullerBehavior> peerIdToBehavior in this.pullerBehaviorsByPeerId)
                peerIdsToTips.Add(peerIdToBehavior.Key, peerIdToBehavior.Value.Tip);

            bool jobFailed = false;
            
            foreach (uint256 hashToAssign in downloadJob.Hashes.Take(emptySlotes).ToList())
            {
                ChainedHeader header = this.headersByHash[hashToAssign];

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

                    ChainedHeader peerTip = peerIdsToTips[peerId];

                    if (peerTip.GetAncestor(header.Height) == header)
                    {
                        // Assign to this peer
                        newAssignments.Add(hashToAssign, new AssignedDownload()
                        {
                            PeerId = peerId,
                            JobId = downloadJob.Id,
                            AssignedTime = DateTime.UtcNow,
                            BlockHeight = header.Height,
                            BlockHash = hashToAssign
                        });

                        this.logger.LogTrace("Block '{0}' was assigned to peer {1}.", hashToAssign, peerTip);

                        downloadJob.Hashes.Remove(hashToAssign);
                        break;
                    }
                    else
                    {
                        // Peer doesn't claim this hash.
                        peerIdsToTips.Remove(peerId);

                        if (peerIdsToTips.Count != 0)
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

                downloadJob.Hashes = new List<uint256>();
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

            var toReassign = new Dictionary<uint256, int>();

            lock (this.assignedLock)
            {
                bool reassigned;

                do
                {
                    reassigned = false;

                    LinkedListNode<AssignedDownload> current = this.assignedDownloadsByHeights.First;

                    while (current != null)
                    {
                        // Since the dictionary is sorted by height after we found first not important block we can assume that the rest of them are not important.
                        if (current.Value.BlockHeight > lastImportantHeight)
                            break;
                        
                        double secondsPassed = (DateTime.UtcNow - current.Value.AssignedTime).TotalSeconds;

                        if (secondsPassed < MaxSecondsToDeliverBlock)
                        {
                            current = current.Next;
                            continue;
                        }

                        // Peer failed to deliver important block. Reassign all his jobs.
                        int peerId = current.Value.PeerId;
                        HashSet<uint256> hashesAssignedToPeer = this.assignedHashesByPeerId[peerId];

                        foreach (uint256 assignedHash in hashesAssignedToPeer)
                        {
                            this.TryRemoveAssignedDownloadLocked(assignedHash, out AssignedDownload removedAssignment);
                            toReassign.Add(assignedHash, removedAssignment.JobId);
                        }
                        
                        int reassignedCount = hashesAssignedToPeer.Count;

                        this.logger.LogDebug("Peer {0} failed to deliver {1} blocks from which some were important.", peerId, reassignedCount);

                        lock (this.peerLock)
                        {
                            BlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];
                            pullerBehavior.Penalize(MaxSecondsToDeliverBlock, reassignedCount);

                            this.RecalculateQuealityScoreLocked(pullerBehavior, peerId);
                        }

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

            lock (this.assignedLock)
            {
                if (!this.assignedDownloads.TryGetValue(blockHash, out assignedDownload))
                {
                    this.logger.LogTrace("(-)[WASNT_REQUESTED]");
                    return;
                }
            }

            if (assignedDownload.PeerId != peerId)
            {
                this.logger.LogTrace("(-)[WRONG_PEER_DELIVERED]");
                return;
            }

            lock (this.queueLock)
            {
                this.pendingDownloadsCount--;

                lock (this.assignedLock)
                {
                    this.TryRemoveAssignedDownloadLocked(blockHash, out AssignedDownload unused);
                }

                this.averageBlockSizeBytes.AddSample(block.BlockSize.Value);

                double deliveredInSeconds = (DateTime.UtcNow - assignedDownload.AssignedTime).TotalSeconds;

                this.logger.LogTrace("Peer {0} delivered block '{1}' in {2} seconds.", assignedDownload.PeerId, blockHash, deliveredInSeconds);

                lock (this.peerLock)
                {
                    // Add peer sample.
                    BlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];
                    pullerBehavior.AddSample(block.BlockSize.Value, deliveredInSeconds);

                    // Recalculate quality score.
                    this.RecalculateQuealityScoreLocked(pullerBehavior, peerId);
                }

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.headersByHash.Remove(blockHash);

                this.processQueuesSignal.Set();
            }
        

            this.OnDownloadedCallback(blockHash, block);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Recalculates quality score of a peer or all peers if given peer has the best upload speed.</summary>
        /// <remarks>Should be protected by <see cref="peerLock"/>.</remarks>
        /// <param name="pullerBehavior">The puller behavior of a peer which quality score should be recalculated.</param>
        /// <param name="peerId">Id of a peer which behavior is passed.</param>
        private void RecalculateQuealityScoreLocked(BlockPullerBehavior pullerBehavior, int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            // Now decide if we need to recalculate quality score for all peers or just for this one.
            int bestSpeed = this.pullerBehaviorsByPeerId.Max(x => x.Value.SpeedBytesPerSecond);

            int adjustedBestSpeed = bestSpeed;
            if (this.ibdState.IsInitialBlockDownload() && (adjustedBestSpeed > PeerSpeedLimitWhenNotInIBDBytesPerSec))
                adjustedBestSpeed = PeerSpeedLimitWhenNotInIBDBytesPerSec;

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

        /// <remarks>Should be protected by <see cref="queueLock"/>.</remarks>
        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            this.logger.LogTrace("()");

            // How many blocks we can download in 1 second.
            if (this.averageBlockSizeBytes.Average > 0)
                this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSec() / this.averageBlockSizeBytes.Average);

            if (this.maxBlocksBeingDownloaded < 10)
                this.maxBlocksBeingDownloaded = 10;

            this.logger.LogTrace("Max amount of blocks that can be downloaded at the same time is set to {0}", this.maxBlocksBeingDownloaded);

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>
        /// Finds all blocks assigned to a given peer, removes assignments
        /// from <see cref="assignedDownloads"/> and adds to <see cref="reassignedJobsQueue"/>.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        private void ReleaseAssignments(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            var hashesToJobIds = new Dictionary<uint256, int>();

            lock (this.assignedLock)
            {
                foreach (uint256 assignedHash in this.assignedHashesByPeerId[peerId].ToList())
                {
                    hashesToJobIds.Add(assignedHash, this.assignedDownloads[assignedHash].JobId);
                    this.TryRemoveAssignedDownloadLocked(assignedHash, out AssignedDownload removedAssignment);
                }
            }

            if (hashesToJobIds.Count != 0)
                this.ReleaseAssignments(hashesToJobIds);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Adds items from <paramref name="hashesToJobIds"/> to the <see cref="reassignedJobsQueue"/>.</summary>
        /// <param name="hashesToJobIds">Block hashes mapped to job ids.</param>
        private void ReleaseAssignments(Dictionary<uint256, int> hashesToJobIds)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(hashesToJobIds), nameof(hashesToJobIds.Count), hashesToJobIds.Count);

            lock (this.queueLock)
            {
                foreach (IGrouping<int, KeyValuePair<uint256, int>> jobGroup in hashesToJobIds.GroupBy(x => x.Value))
                {
                    var newJob = new DownloadJob()
                    {
                        Id = jobGroup.First().Value,
                        Hashes = new List<uint256>(jobGroup.Select(x => x.Key))
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
            public List<uint256> Hashes;
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
            
            /// <summary>Hash or the requested block.</summary>
            public uint256 BlockHash;

            /// <inheritdoc />
            public override string ToString()
            {
                return string.Format("{0}:{1},{2}:{3},{4}:{5}", nameof(this.JobId), this.JobId, nameof(this.PeerId), this.PeerId, nameof(this.BlockHeight), this.BlockHeight);
            }
        }
    }
}
