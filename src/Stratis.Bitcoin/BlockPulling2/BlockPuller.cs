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

//TODO check that hashes are consequtive everywhere where we need the to be
namespace Stratis.Bitcoin.BlockPulling2
{
    /// <summary>
    /// Thread-safe block puller which allows downloading blocks from all chains that the node is aware of.
    /// </summary>
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
    /// <para>
    /// We never wait for the same block to be delivered from more than 1 peer at once, so in case peer was removed from the assignment
    /// and delivered after that we will discard delivered block from this peer.
    /// </para>
    /// </remarks>
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
        private const int MaxSecondsToDeliverBlock = 30; //TODO change to target spacing / 3

        /// <summary>This affects quality score only. If the peer is too fast don't give him all the assignments in the world when not in IBD.</summary>
        private const int PeerSpeedLimitWhenNotInIbdBytesPerSec = 1024 * 1024;
        
        /// <param name="blockHash">Hash of the delivered block.</param>
        /// <param name="block">The block.</param>
        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block);

        /// <summary>Callback which is called when puller received a block which it was asked for.</summary>
        /// <remarks>Provided by the component that creates the block puller.</remarks>
        private readonly OnBlockDownloadedCallback OnDownloadedCallback;

        /// <summary>Queue of download jobs which were released from the peers that failed to deliver in time or were disconnected.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private readonly Queue<DownloadJob> reassignedJobsQueue;

        /// <summary>Queue of download jobs which should be assigned to peers.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private readonly Queue<DownloadJob> downloadJobsQueue;

        /// <summary>Collection of all download assignments to the peers sorted by block height.</summary>
        /// <remarks>This object has to be protected by <see cref="assignedLock"/>.</remarks>
        private readonly Dictionary<uint256, AssignedDownload> assignedDownloads;

        /// <summary>Assigned downloads sorted by block height.</summary>
        /// <remarks>This object has to be protected by <see cref="assignedLock"/>.</remarks>
        private readonly LinkedList<AssignedDownload> sortedAssignedDownloads;

        /// <summary>Assigned headers mapped by peer ID.</summary>
        /// <remarks>This object has to be protected by <see cref="assignedLock"/>.</remarks>
        private readonly Dictionary<int, List<ChainedHeader>> assignedHeadersByPeerId;

        /// <summary>Block puller behaviors mapped by peer ID.</summary>
        /// <remarks>This object has to be protected by <see cref="peerLock"/>.</remarks>
        private readonly Dictionary<int, BlockPullerBehavior> pullerBehaviorsByPeerId;

        private readonly CancellationTokenSource cancellationSource;

        /// <summary>The average block size in bytes calculated used up to <see cref="AverageBlockSizeSamplesCount"/> most recent samples.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock" />.</remarks>
        private readonly AverageCalculator averageBlockSizeBytes;

        /// <summary>Amount of samples that should be used for average block size calculation.</summary>
        private const int AverageBlockSizeSamplesCount = 1000;

        /// <summary>The minimal count of blocks that we can ask for simultaneous download.</summary>
        private const int MinimalCountOfBlocksBeingDownloaded = 10;

        /// <summary>Signaler that triggers <see cref="reassignedJobsQueue"/> and <see cref="downloadJobsQueue"/> processing when set.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private readonly AsyncManualResetEvent processQueuesSignal;

        /// <summary>Unique identifier which will be set to the next created download job.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private int nextJobId;

        /// <summary>Locks access to <see cref="pullerBehaviorsByPeerId"/>.</summary>
        private readonly object peerLock;

        /// <summary>
        /// Locks access to <see cref="headersByHash"/>, <see cref="processQueuesSignal"/>, <see cref="downloadJobsQueue"/>, <see cref="reassignedJobsQueue"/>,
        /// <see cref="maxBlocksBeingDownloaded"/>, <see cref="nextJobId"/>, <see cref="averageBlockSizeBytes"/>.
        /// </summary>
        private readonly object queueLock;

        /// <summary>Locks access to <see cref="assignedDownloads"/>, <see cref="assignedHeadersByPeerId"/>, <see cref="sortedAssignedDownloads"/>.</summary>
        private readonly object assignedLock;

        /// <summary>
        /// The maximum blocks that can be downloaded simultaneously.
        /// Given that all peers are on the same chain they will deliver that amount of blocks in 1 seconds.
        /// </summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private int maxBlocksBeingDownloaded;

        /// <summary><c>true</c> if node is in IBD.</summary>
        /// <remarks>This object has to be protected by <see cref="peerLock"/>.</remarks>
        private bool isIbd;

        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        /// <inheritdoc cref="ChainState"/>
        private readonly ChainState chainState;

        /// <inheritdoc cref="NetworkPeerRequirement"/>
        private readonly NetworkPeerRequirement networkPeerRequirement;
        
        /// <inheritdoc cref="IDateTimeProvider"/>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <inheritdoc cref="random"/>
        private readonly Random random;

        /// <summary>Loop that assigns download jobs to the peers.</summary>
        private Task assignerLoop;

        /// <summary>Loop that checks if peers failed to deliver important blocks in given time and penalizes them if they did.</summary>
        private Task stallingLoop;

        public BlockPuller(OnBlockDownloadedCallback callback, ChainState chainState, ProtocolVersion protocolVersion, IDateTimeProvider dateTimeProvider, LoggerFactory loggerFactory)
        {
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.assignedDownloads = new Dictionary<uint256, AssignedDownload>();
            this.sortedAssignedDownloads = new LinkedList<AssignedDownload>();
            this.assignedHeadersByPeerId = new Dictionary<int, List<ChainedHeader>>();

            this.averageBlockSizeBytes = new AverageCalculator(AverageBlockSizeSamplesCount);
            
            this.pullerBehaviorsByPeerId = new Dictionary<int, BlockPullerBehavior>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.queueLock = new object();
            this.peerLock = new object();
            this.assignedLock = new object();
            this.nextJobId = 0;
            
            this.networkPeerRequirement = new NetworkPeerRequirement
            {
                MinVersion = protocolVersion,
                RequiredServices = NetworkPeerServices.Network
            };

            this.cancellationSource = new CancellationTokenSource();
            this.random = new Random();

            this.OnDownloadedCallback = callback;
            this.chainState = chainState;
            this.dateTimeProvider = dateTimeProvider;
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

        /// <summary>Updates puller behaviors when IDB state is changed.</summary>
        /// <remarks>Should be called when IBD state was changed or first calculated.</remarks>
        public void OnIbdStateChanged(bool isIbd)
        {
            this.logger.LogTrace("({0}:{1})", nameof(isIbd), isIbd);

            lock (this.peerLock)
            {
                foreach (BlockPullerBehavior blockPullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    blockPullerBehavior.OnIbdStateChanged(isIbd);

                this.isIbd = isIbd;
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Updates puller's view of peer's tip.</summary>
        /// <remarks>Should be called when a peer claims a new tip.</remarks>
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
                    this.logger.LogTrace("Tip for peer with ID {0} was changed to '{1}'.", peerId, newTip);
                }
                else
                {
                    bool supportsRequirments = this.networkPeerRequirement.Check(peer.PeerVersion);

                    if (supportsRequirments)
                    {
                        var behavior = peer.Behavior<BlockPullerBehavior>();
                        behavior.Tip = newTip;
                        this.pullerBehaviorsByPeerId.Add(peerId, behavior);

                        this.logger.LogTrace("New peer with ID {0} and tip '{1}' was added.", peerId, newTip);
                    }
                    else
                        this.logger.LogTrace("Peer ID {0} was discarded since he doesn't support the requirements.", peerId);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>Removes information about the peer from the inner structures.</summary>
        /// <remarks>Adds download jobs that were assigned to this peer to reassign queue.</remarks>
        /// <param name="peerId">Unique peer identifier.</param>
        public void PeerDisconnected(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            lock (this.peerLock)
            {
                this.pullerBehaviorsByPeerId.Remove(peerId);
            }

            this.ReleaseAndReassignAssignments(peerId);

            lock (this.queueLock)
            {
                this.processQueuesSignal.Set();
            }

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
                // Enqueue new download job.
                int jobId = this.nextJobId++;

                this.downloadJobsQueue.Enqueue(new DownloadJob()
                {
                    Headers = headers,
                    Id = jobId
                });

                this.processQueuesSignal.Set();
                this.logger.LogDebug("{0} blocks were requested from puller. Job ID {1} was created.", headers.Count, jobId);
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
            var newAssignments = new List<AssignedDownload>();

            lock (this.queueLock)
            {
                // First process reassign queue ignoring slots limitations.
                while (this.reassignedJobsQueue.Count > 0)
                {
                    DownloadJob jobToReassign = this.reassignedJobsQueue.Dequeue();
                    this.logger.LogDebug("Reassigning job {0} with {1} headers.", jobToReassign.Id, jobToReassign.Headers.Count);

                    List<AssignedDownload> assignments;

                    lock (this.peerLock)
                    {
                        assignments = this.DistributeHeadersLocked(ref jobToReassign, ref failedJobs, int.MaxValue);
                    }

                    lock (this.assignedLock)
                    {
                        foreach (AssignedDownload assignment in assignments)
                        {
                            newAssignments.Add(assignment);
                            this.AddAssignedDownloadLocked(assignment);
                        }
                    }
                }

                // Process regular queue.
                int emptySlots = 0;
                lock (this.assignedLock)
                {
                    emptySlots = this.maxBlocksBeingDownloaded - this.assignedDownloads.Count;
                }

                this.logger.LogTrace("There are {0} empty slots.", emptySlots);

                if (emptySlots > this.maxBlocksBeingDownloaded / 100.0 * MinEmptySlotsPercentageToStartProcessingTheQueue)
                {
                    while ((this.downloadJobsQueue.Count > 0) && (emptySlots > 0))
                    {
                        DownloadJob jobToAassign = this.downloadJobsQueue.Peek();
                        int jobHeadersCount = jobToAassign.Headers.Count;

                        List<AssignedDownload> assignments;

                        lock (this.peerLock)
                        {
                            assignments = this.DistributeHeadersLocked(ref jobToAassign, ref failedJobs, emptySlots);
                        }

                        emptySlots -= assignments.Count;
                        this.logger.LogDebug("Assigned {0} headers out of {1} for job {2}", assignments.Count, jobHeadersCount, jobToAassign.Id);

                        lock (this.assignedLock)
                        {
                            foreach (AssignedDownload assignment in assignments)
                            {
                                newAssignments.Add(assignment);
                                this.AddAssignedDownloadLocked(assignment);
                            }
                        }

                        // Remove job from the queue if it was fully consumed.
                        if (jobToAassign.Headers.Count == 0)
                            this.downloadJobsQueue.Dequeue();
                    }
                }
                
                this.processQueuesSignal.Reset();

                this.logger.LogTrace("Total amount of downloads assigned in this iteration is {0}.", newAssignments.Count);
            }
            
            // Call callbacks with null since puller failed to deliver requested blocks.
            foreach (DownloadJob failedJob in failedJobs)
            {
                foreach (ChainedHeader failedHeader in failedJob.Headers)
                    this.OnDownloadedCallback(failedHeader.HashBlock, null);
            }

            if (newAssignments.Count != 0)
                await this.AskPeersForBlocksAsync(newAssignments).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adds assigned download to <see cref="assignedDownloads"/> and helper structures <see cref="sortedAssignedDownloads"/> and <see cref="assignedHeadersByPeerId"/>.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="assignedLock"/>.</remarks>
        /// <param name="assignment">The assignment.</param>
        private void AddAssignedDownloadLocked(AssignedDownload assignment)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(assignment), assignment);

            this.assignedDownloads.Add(assignment.Header.HashBlock, assignment);

            // Add to assignedHeadersByPeerId.
            if (!this.assignedHeadersByPeerId.TryGetValue(assignment.PeerId, out List<ChainedHeader> headersForIds))
            {
                headersForIds = new List<ChainedHeader>();
                this.assignedHeadersByPeerId.Add(assignment.PeerId, headersForIds);
            }

            headersForIds.Add(assignment.Header);

            // Add to sortedAssignedDownloads.
            LinkedListNode<AssignedDownload> lastDownload = this.sortedAssignedDownloads.Last;

            if ((lastDownload == null) || (lastDownload.Value.Header.Height <= assignment.Header.Height))
            {
                assignment.LinkedListNode = this.sortedAssignedDownloads.AddLast(assignment);
            }
            else
            {
                LinkedListNode<AssignedDownload> current = lastDownload.Previous;

                while (current.Value.Header.Height > assignment.Header.Height)
                    current = current.Previous;

                assignment.LinkedListNode = this.sortedAssignedDownloads.AddAfter(current, assignment);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Removes assigned download from <see cref="assignedDownloads"/> and helper structures <see cref="sortedAssignedDownloads"/> and <see cref="assignedHeadersByPeerId"/>.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="assignedLock"/>.</remarks>
        /// <param name="header">Header of a block for which assignment should be removed.</param>
        /// <returns>The assignment that corresponds to the provided hash or <c>null</c> if assignment wasn't found.</returns>
        private AssignedDownload RemoveAssignedDownloadLocked(ChainedHeader header)
        {
            this.logger.LogTrace("({0}:'{1}')", nameof(header), header);

            AssignedDownload assignment = this.assignedDownloads[header.HashBlock];

            this.assignedDownloads.Remove(header.HashBlock);

            List<ChainedHeader> headersForId = this.assignedHeadersByPeerId[assignment.PeerId];
            headersForId.Remove(header);
            if (headersForId.Count == 0)
                this.assignedHeadersByPeerId.Remove(assignment.PeerId);
          
            this.sortedAssignedDownloads.Remove(assignment.LinkedListNode);
            
            this.logger.LogTrace("(-):'{0}'", assignment);
            return assignment;
        }
        
        /// <summary>Asks peer behaviors in parallel to deliver blocks.</summary>
        /// <param name="assignments">Assignments given to peers.</param>
        private async Task AskPeersForBlocksAsync(List<AssignedDownload> assignments)
        {
            this.logger.LogTrace("({0}:{1})", nameof(assignments.Count), assignments.Count);

            // Form batches in order to ask for several blocks from one peer at once.
            var hashesToPeerId = new Dictionary<int, List<uint256>>();
            foreach (AssignedDownload assignedDownload in assignments)
            {
                if (!hashesToPeerId.TryGetValue(assignedDownload.PeerId, out List<uint256> hashes))
                {
                    hashes = new List<uint256>();
                    hashesToPeerId.Add(assignedDownload.PeerId, hashes);
                }

                hashes.Add(assignedDownload.Header.HashBlock);
            }

            foreach (KeyValuePair<int, List<uint256>> hashesPair in hashesToPeerId)
            {
                List<uint256> hashes = hashesPair.Value;
                int peerId = hashesPair.Key;

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
                    this.PeerDisconnected(peerId);
                }
            }
            
            this.logger.LogTrace("(-)");
        }

        /// <summary>Distributes headers from a provided download jobs between peers that can provide blocks represented by those headers.</summary>
        /// <remarks>
        /// If some of the blocks from the job can't be provided by any peer those headers will be added to a <param name="failedJobs"> as a new item.</param>
        /// <para>
        /// Have to be locked by <see cref="peerLock"/> and <see cref="queueLock"/>.
        /// </para>
        /// </remarks>
        /// <param name="downloadJob">Download job to be partially of fully consumed.</param>
        /// <param name="failedJobs">Failed assignments.</param>
        /// <param name="emptySlotes">Amount of empty slots. This is the maximum amount of assignments that can be created.</param>
        private List<AssignedDownload> DistributeHeadersLocked(ref DownloadJob downloadJob, ref List<DownloadJob> failedJobs, int emptySlotes)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3},{4}:{5})", nameof(downloadJob.Headers.Count), downloadJob.Headers.Count, nameof(failedJobs.Count), failedJobs.Count, nameof(emptySlotes), emptySlotes);

            var newAssignments = new List<AssignedDownload>();
            
            var peerIdsToTips = new Dictionary<int, ChainedHeader>(this.pullerBehaviorsByPeerId.Count);
            foreach (KeyValuePair<int, BlockPullerBehavior> peerIdToBehavior in this.pullerBehaviorsByPeerId)
                peerIdsToTips.Add(peerIdToBehavior.Key, peerIdToBehavior.Value.Tip);

            bool jobFailed = false;
            
            foreach (ChainedHeader header in downloadJob.Headers.Take(emptySlotes).ToList())
            {
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
                        newAssignments.Add(new AssignedDownload()
                        {
                            PeerId = peerId,
                            JobId = downloadJob.Id,
                            AssignedTime = this.dateTimeProvider.GetUtcNow(),
                            Header = header
                        });

                        this.logger.LogTrace("Block '{0}' was assigned to peer ID {1}.", header.HashBlock, peerId);

                        downloadJob.Headers.Remove(header);
                        break;
                    }
                    else
                    {
                        // Peer doesn't claim this header.
                        peerIdsToTips.Remove(peerId);

                        if (peerIdsToTips.Count != 0)
                            continue;

                        jobFailed = true;
                        this.logger.LogDebug("Job {0} failed because there is no peer claiming {1} of it's headers.", downloadJob.Id, downloadJob.Headers.Count);
                    }
                }
            }

            if (jobFailed)
            {
                failedJobs.Add(new DownloadJob() { Headers = downloadJob.Headers });
                downloadJob.Headers = new List<ChainedHeader>();
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

            bool reassigned = false;

            lock (this.assignedLock)
            {
                LinkedListNode<AssignedDownload> current = this.sortedAssignedDownloads.First;

                var peerIdsToReassignJobs = new HashSet<int>();

                while (current != null)
                {
                    // Since the dictionary is sorted by height after we found first not important block we can assume that the rest of them are not important.
                    if (current.Value.Header.Height > lastImportantHeight)
                        break;
                    
                    double secondsPassed = (this.dateTimeProvider.GetUtcNow() - current.Value.AssignedTime).TotalSeconds;

                    if (secondsPassed < MaxSecondsToDeliverBlock)
                    {
                        current = current.Next;
                        continue;
                    }

                    // Peer failed to deliver important block.
                    int peerId = current.Value.PeerId;

                    if (peerIdsToReassignJobs.Contains(peerId))
                    {
                        // Peer already added to the collection of peers to release and reassign.
                        continue;
                    }

                    peerIdsToReassignJobs.Add(peerId);

                    int assignedCount = this.assignedHeadersByPeerId[peerId].Count;

                    this.logger.LogDebug("Peer {0} failed to deliver {1} blocks from which some were important.", peerId, assignedCount);

                    lock (this.peerLock)
                    {
                        BlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];
                        pullerBehavior.Penalize(MaxSecondsToDeliverBlock, assignedCount);

                        this.RecalculateQuealityScoreLocked(pullerBehavior, peerId);
                    } 
                }

                var allReleasedAssignments = new HashSet<Dictionary<int, List<ChainedHeader>>>();

                // Release downloads for selected peers.
                foreach (int peerId in peerIdsToReassignJobs)
                {
                    Dictionary<int, List<ChainedHeader>> reassignedAssignments = this.ReleaseAssignmentsLocked(peerId);
                    allReleasedAssignments.Add(reassignedAssignments);
                }

                // Reassign all released jobs.
                foreach (Dictionary<int, List<ChainedHeader>> released in allReleasedAssignments)
                    this.ReassignAssignments(released);

                reassigned = allReleasedAssignments.Count > 0;
            }

            if (reassigned)
            {
                lock (this.queueLock)
                {
                    // Trigger queue processing in case anything was reassigned.
                    this.processQueuesSignal.Set();
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Method which is called when <see cref="BlockPullerBehavior"/> receives a block.</summary>
        /// <remarks>
        /// This method is called for all blocks that were delivered. It is possible that block that wasn't requested
        /// from that peer or from any peer at all is delivered, in that case the block will be ignored.
        /// It is possible that block was reassigned from a peer who delivered it later, in that case it will be ignored from this peer.
        /// </remarks>
        /// <param name="blockHash">The block hash.</param>
        /// <param name="block">The block.</param>
        /// <param name="peerId">ID of a peer that delivered a block.</param>
        public void PushBlock(uint256 blockHash, Block block, int peerId)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:{3})", nameof(blockHash), blockHash, nameof(peerId), peerId);

            AssignedDownload assignedDownload;

            lock (this.assignedLock)
            {
                if (!this.assignedDownloads.TryGetValue(blockHash, out assignedDownload))
                {
                    this.logger.LogTrace("(-)[BLOCK_NOT_REQUESTED]");
                    return;
                }

                this.logger.LogTrace("Assignment '{0}' for peer ID {1} was delivered by peer ID {2}.", blockHash, assignedDownload.PeerId, peerId);
                
                if (assignedDownload.PeerId != peerId)
                {
                    this.logger.LogTrace("(-)[WRONG_PEER_DELIVERED]");
                    return;
                }

                this.RemoveAssignedDownloadLocked(assignedDownload.Header);
            }

            double deliveredInSeconds = (this.dateTimeProvider.GetUtcNow() - assignedDownload.AssignedTime).TotalSeconds;
            this.logger.LogTrace("Peer {0} delivered block '{1}' in {2} seconds.", assignedDownload.PeerId, blockHash, deliveredInSeconds);

            lock (this.peerLock)
            {
                // Add peer sample.
                BlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];
                pullerBehavior.AddSample(block.BlockSize.Value, deliveredInSeconds);

                // Recalculate quality score.
                this.RecalculateQuealityScoreLocked(pullerBehavior, peerId);
            }

            lock (this.queueLock)
            {
                this.averageBlockSizeBytes.AddSample(block.BlockSize.Value);

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.processQueuesSignal.Set();
            }

            this.OnDownloadedCallback(blockHash, block);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Recalculates quality score of a peer or all peers if given peer has the best upload speed.</summary>
        /// <remarks>This method has to be protected by <see cref="peerLock"/>.</remarks>
        /// <param name="pullerBehavior">The puller behavior of a peer which quality score should be recalculated.</param>
        /// <param name="peerId">ID of a peer which behavior is passed.</param>
        private void RecalculateQuealityScoreLocked(BlockPullerBehavior pullerBehavior, int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            // Now decide if we need to recalculate quality score for all peers or just for this one.
            int bestSpeed = this.pullerBehaviorsByPeerId.Max(x => x.Value.SpeedBytesPerSecond);

            int adjustedBestSpeed = bestSpeed;
            if (!this.isIbd && (adjustedBestSpeed > PeerSpeedLimitWhenNotInIbdBytesPerSec))
                adjustedBestSpeed = PeerSpeedLimitWhenNotInIbdBytesPerSec;

            if (pullerBehavior.SpeedBytesPerSecond != bestSpeed)
            {
                // This is not the best peer. Recalculate it's score only.
                pullerBehavior.RecalculateQualityScore(adjustedBestSpeed);
            }
            else
            {
                this.logger.LogTrace("Peer ID {0} is the fastest peer. Recalculating quality score of all peers.", peerId);

                // This is the best peer. Recalculate quality score for everyone.
                foreach (BlockPullerBehavior peerPullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    peerPullerBehavior.RecalculateQualityScore(adjustedBestSpeed);
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Recalculates the maximum number of blocks that can be simultaneously downloaded based
        /// on the average blocks size and the total speed of all peers that can deliver blocks.
        /// </summary>
        /// <remarks>This object has to be protected by <see cref="queueLock" />.</remarks>
        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            this.logger.LogTrace("()");

            // How many blocks we can download in 1 second.
            if (this.averageBlockSizeBytes.Average > 0)
                this.maxBlocksBeingDownloaded = (int)(this.GetTotalSpeedOfAllPeersBytesPerSec() / this.averageBlockSizeBytes.Average);

            if (this.maxBlocksBeingDownloaded < MinimalCountOfBlocksBeingDownloaded)
                this.maxBlocksBeingDownloaded = MinimalCountOfBlocksBeingDownloaded;

            this.logger.LogTrace("Max number of blocks that can be downloaded at the same time is set to {0}.", this.maxBlocksBeingDownloaded);
            this.logger.LogTrace("(-)");
        }
        
        /// <summary>
        /// Finds all blocks assigned to a given peer, removes assignments from <see cref="assignedDownloads"/> and adds to <see cref="reassignedJobsQueue"/>.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        private void ReleaseAndReassignAssignments(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            Dictionary<int, List<ChainedHeader>> headersByJobId;

            lock (this.assignedLock)
            {
                headersByJobId = this.ReleaseAssignmentsLocked(peerId);
            }

            if (headersByJobId.Count != 0)
                this.ReassignAssignments(headersByJobId);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Finds all blocks assigned to a given peer, removes assignments from <see cref="assignedDownloads"/> and returns removed assignments.</summary>
        /// <remarks>Have to be locked by <see cref="assignedLock"/>.</remarks>
        private Dictionary<int, List<ChainedHeader>> ReleaseAssignmentsLocked(int peerId)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerId), peerId);

            var headersByJobId = new Dictionary<int, List<ChainedHeader>>();

            if (this.assignedHeadersByPeerId.TryGetValue(peerId, out List<ChainedHeader> headers))
            {
                for (int i = headers.Count - 1; i >= 0; i--)
                {
                    ChainedHeader chainedHeader = headers[i];

                    int jobId = this.assignedDownloads[chainedHeader.HashBlock].JobId;

                    if (!headersByJobId.TryGetValue(jobId, out List<ChainedHeader> jobHeaders))
                    {
                        jobHeaders = new List<ChainedHeader>();
                        headersByJobId.Add(jobId, jobHeaders);
                    }

                    // Add in reverse order. Fix the order later.
                    jobHeaders.Add(chainedHeader);

                    this.RemoveAssignedDownloadLocked(chainedHeader);

                    this.logger.LogTrace("Header '{0}' for job ID {1} was released from peer ID {2}.", chainedHeader, jobId, peerId);
                }
            }

            // Make sure that headers are consecutive.
            foreach (KeyValuePair<int, List<ChainedHeader>> headersById in headersByJobId)
                headersById.Value.Reverse();

            this.logger.LogTrace("(-):*.{0}={1}", nameof(headersByJobId.Count), headersByJobId.Count);
            return headersByJobId;
        }

        /// <summary>Adds items from <paramref name="headersByJobId"/> to the <see cref="reassignedJobsQueue"/>.</summary>
        /// <param name="headersByJobId">Block headers mapped by job IDs.</param>
        private void ReassignAssignments(Dictionary<int, List<ChainedHeader>> headersByJobId)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(headersByJobId), nameof(headersByJobId.Count), headersByJobId.Count);

            lock (this.queueLock)
            {
                foreach (KeyValuePair<int, List<ChainedHeader>> jobIdToHeaders in headersByJobId)
                {
                    var newJob = new DownloadJob()
                    {
                        Id = jobIdToHeaders.Key,
                        Headers = jobIdToHeaders.Value
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
            just for logging
	        show it as a part of nodestats, not separated spammer:
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

        /// <summary>Represents consecutive collection of headers that are to be downloaded.</summary>
        private struct DownloadJob
        {
            /// <summary>Unique identifier of this job.</summary>
            public int Id;

            /// <summary>Headers of blocks that are to be downloaded.</summary>
            public List<ChainedHeader> Headers;
        }
    }
}
