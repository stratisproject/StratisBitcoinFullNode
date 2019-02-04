using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling
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
    public interface IBlockPuller : IDisposable
    {
        void Initialize(BlockPuller.OnBlockDownloadedCallback callback);

        /// <summary>
        /// Adds required services to list of services that are required from all peers.
        /// </summary>
        /// <remarks>
        /// In case some of the peers that we are already requesting block from don't support new
        /// service requirements those peers will be released from their assignments.
        /// </remarks>
        void RequestPeerServices(NetworkPeerServices services);

        /// <summary>Gets the average size of a block based on sizes of blocks that were previously downloaded.</summary>
        double GetAverageBlockSizeBytes();

        /// <summary>Updates puller behaviors when IDB state is changed.</summary>
        /// <remarks>Should be called when IBD state was changed or first calculated.</remarks>
        void OnIbdStateChanged(bool isIbd);

        /// <summary>Updates puller's view of peer's tip.</summary>
        /// <remarks>Should be called when a peer claims a new tip.</remarks>
        /// <param name="peer">The peer.</param>
        /// <param name="newTip">New tip.</param>
        void NewPeerTipClaimed(INetworkPeer peer, ChainedHeader newTip);

        /// <summary>Removes information about the peer from the inner structures.</summary>
        /// <remarks>Adds download jobs that were assigned to this peer to reassign queue.</remarks>
        /// <param name="peerId">Unique peer identifier.</param>
        void PeerDisconnected(int peerId);

        /// <summary>Requests the blocks for download.</summary>
        /// <remarks>Doesn't support asking for the same hash twice before getting a response.</remarks>
        /// <param name="headers">Collection of consecutive headers (but gaps are ok: a1=a2=a3=a4=a8=a9).</param>
        /// <param name="highPriority">If <c>true</c> headers will be assigned to peers before the headers that were asked normally.</param>
        void RequestBlocksDownload(List<ChainedHeader> headers, bool highPriority = false);

        /// <summary>Removes assignments for the block which has been delivered by the peer assigned to it and calls the callback.</summary>
        /// <remarks>
        /// This method is called for all blocks that were delivered. It is possible that block that wasn't requested
        /// from that peer or from any peer at all is delivered, in that case the block will be ignored.
        /// It is possible that block was reassigned from a peer who delivered it later, in that case it will be ignored from this peer.
        /// </remarks>
        /// <param name="blockHash">The block hash.</param>
        /// <param name="block">The block.</param>
        /// <param name="peerId">ID of a peer that delivered a block.</param>
        void PushBlock(uint256 blockHash, Block block, int peerId);
    }

    public class BlockPuller : IBlockPuller
    {
        /// <summary>Interval between checking if peers that were assigned important blocks didn't deliver the block.</summary>
        private const int StallingLoopIntervalMs = 500;

        /// <summary>The minimum empty slots percentage to start processing <see cref="downloadJobsQueue"/>.</summary>
        private const double MinEmptySlotsPercentageToStartProcessingTheQueue = 0.1;

        /// <summary>
        /// Defines which blocks are considered to be important.
        /// If requested block height is less than out consensus tip height plus this value then the block is considered to be important.
        /// </summary>
        private const int ImportantHeightMargin = 10;

        /// <summary>The maximum time in seconds in which peer should deliver an assigned block.</summary>
        /// <remarks>If peer fails to deliver in that time his assignments will be released and the peer penalized.</remarks>
        private const int MaxSecondsToDeliverBlock = 30; // TODO change to target spacing / 3

        /// <summary>This affects quality score only. If the peer is too fast don't give him all the assignments in the world when not in IBD.</summary>
        private const int PeerSpeedLimitWhenNotInIbdBytesPerSec = 1024 * 1024;

        /// <param name="blockHash">Hash of the delivered block.</param>
        /// <param name="block">The block.</param>
        /// <param name="peerId">The ID of a peer that delivered the block.</param>
        public delegate void OnBlockDownloadedCallback(uint256 blockHash, Block block, int peerId);

        /// <summary>Callback which is called when puller received a block which it was asked for.</summary>
        /// <remarks>Provided by the component that creates the block puller.</remarks>
        private OnBlockDownloadedCallback onDownloadedCallback;

        /// <summary>Queue of download jobs which were released from the peers that failed to deliver in time or were disconnected.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private readonly Queue<DownloadJob> reassignedJobsQueue;

        /// <summary>Queue of download jobs which should be assigned to peers.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private readonly Queue<DownloadJob> downloadJobsQueue;

        /// <summary>Collection of all download assignments to the peers sorted by block height.</summary>
        /// <remarks>This object has to be protected by <see cref="assignedLock"/>.</remarks>
        private readonly Dictionary<uint256, AssignedDownload> assignedDownloadsByHash;

        /// <summary>Assigned downloads sorted by block height.</summary>
        /// <remarks>This object has to be protected by <see cref="assignedLock"/>.</remarks>
        private readonly LinkedList<AssignedDownload> assignedDownloadsSorted;

        /// <summary>Assigned headers mapped by peer ID.</summary>
        /// <remarks>This object has to be protected by <see cref="assignedLock"/>.</remarks>
        private readonly Dictionary<int, List<ChainedHeader>> assignedHeadersByPeerId;

        /// <summary>Block puller behaviors mapped by peer ID.</summary>
        /// <remarks>This object has to be protected by <see cref="peerLock"/>.</remarks>
        private readonly Dictionary<int, IBlockPullerBehavior> pullerBehaviorsByPeerId;

        /// <summary>The cancellation source that indicates that component's shutdown was triggered.</summary>
        private readonly CancellationTokenSource cancellationSource;

        /// <summary>The average block size in bytes calculated used up to <see cref="AverageBlockSizeSamplesCount"/> most recent samples.</summary>
        /// <remarks>Write access to this object has to be protected by <see cref="queueLock" />.</remarks>
        private readonly AverageCalculator averageBlockSizeBytes;

        /// <summary>Amount of samples that should be used for average block size calculation.</summary>
        private const int AverageBlockSizeSamplesCount = 1000;

        /// <summary>The minimal count of blocks that we can ask for simultaneous download.</summary>
        private const int MinimalCountOfBlocksBeingDownloaded = 10;

        /// <summary>The maximum blocks being downloaded multiplier. Value of <c>1.1</c> means that we will ask for 10% more than we estimated peers can deliver.</summary>
        private const double MaxBlocksBeingDownloadedMultiplier = 1.1;

        /// <summary>Signaler that triggers <see cref="reassignedJobsQueue"/> and <see cref="downloadJobsQueue"/> processing when set.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private readonly AsyncManualResetEvent processQueuesSignal;

        /// <summary>Unique identifier which will be set to the next created download job.</summary>
        /// <remarks>This object has to be protected by <see cref="queueLock"/>.</remarks>
        private int nextJobId;

        /// <summary>Locks access to <see cref="pullerBehaviorsByPeerId"/> and <see cref="networkPeerRequirement"/>.</summary>
        private readonly object peerLock;

        /// <summary>
        /// Locks access to <see cref="processQueuesSignal"/>, <see cref="downloadJobsQueue"/>, <see cref="reassignedJobsQueue"/>,
        /// <see cref="maxBlocksBeingDownloaded"/>, <see cref="nextJobId"/>, <see cref="averageBlockSizeBytes"/>.
        /// </summary>
        private readonly object queueLock;

        /// <summary>Locks access to <see cref="assignedDownloadsByHash"/>, <see cref="assignedHeadersByPeerId"/>, <see cref="assignedDownloadsSorted"/>.</summary>
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

        /// <inheritdoc cref="IChainState"/>
        private readonly IChainState chainState;

        /// <inheritdoc cref="NetworkPeerRequirement"/>
        /// <remarks>This object has to be protected by <see cref="peerLock"/>.</remarks>
        private readonly NetworkPeerRequirement networkPeerRequirement;

        /// <inheritdoc cref="IDateTimeProvider"/>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <inheritdoc cref="Random"/>
        private readonly Random random;

        /// <summary>Loop that assigns download jobs to the peers.</summary>
        private Task assignerLoop;

        /// <summary>Loop that checks if peers failed to deliver important blocks in given time and penalizes them if they did.</summary>
        private Task stallingLoop;

        public BlockPuller(IChainState chainState, NodeSettings nodeSettings, IDateTimeProvider dateTimeProvider, INodeStats nodeStats, ILoggerFactory loggerFactory)
        {
            this.reassignedJobsQueue = new Queue<DownloadJob>();
            this.downloadJobsQueue = new Queue<DownloadJob>();

            this.assignedDownloadsByHash = new Dictionary<uint256, AssignedDownload>();
            this.assignedDownloadsSorted = new LinkedList<AssignedDownload>();
            this.assignedHeadersByPeerId = new Dictionary<int, List<ChainedHeader>>();

            this.averageBlockSizeBytes = new AverageCalculator(AverageBlockSizeSamplesCount);

            this.pullerBehaviorsByPeerId = new Dictionary<int, IBlockPullerBehavior>();

            this.processQueuesSignal = new AsyncManualResetEvent(false);
            this.queueLock = new object();
            this.peerLock = new object();
            this.assignedLock = new object();
            this.nextJobId = 0;

            this.networkPeerRequirement = new NetworkPeerRequirement
            {
                MinVersion = nodeSettings.MinProtocolVersion ?? nodeSettings.ProtocolVersion,
                RequiredServices = NetworkPeerServices.Network
            };

            this.cancellationSource = new CancellationTokenSource();
            this.random = new Random();

            this.maxBlocksBeingDownloaded = MinimalCountOfBlocksBeingDownloaded;

            this.chainState = chainState;
            this.dateTimeProvider = dateTimeProvider;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
        }

        /// <inheritdoc/>
        public void Initialize(OnBlockDownloadedCallback callback)
        {
            this.onDownloadedCallback = callback;

            this.assignerLoop = this.AssignerLoopAsync();
            this.stallingLoop = this.StallingLoopAsync();
        }

        /// <inheritdoc />
        public void RequestPeerServices(NetworkPeerServices services)
        {
            var peerIdsToRemove = new List<int>();

            lock (this.peerLock)
            {
                this.networkPeerRequirement.RequiredServices |= services;

                foreach (KeyValuePair<int, IBlockPullerBehavior> peerIdToBehavior in this.pullerBehaviorsByPeerId)
                {
                    INetworkPeer peer = peerIdToBehavior.Value.AttachedPeer;
                    string reason = string.Empty;

                    if ((peer == null) || !this.networkPeerRequirement.Check(peer.PeerVersion, out reason))
                    {
                        this.logger.LogDebug("Peer Id {0} does not meet requirements, reason: {1}", peerIdToBehavior.Key, reason);
                        peerIdsToRemove.Add(peerIdToBehavior.Key);
                    }
                }
            }

            foreach (int peerId in peerIdsToRemove)
                this.PeerDisconnected(peerId);
        }

        /// <inheritdoc/>
        public double GetAverageBlockSizeBytes()
        {
            return this.averageBlockSizeBytes.Average;
        }

        private long GetTotalSpeedOfAllPeersBytesPerSec()
        {
            lock (this.peerLock)
            {
                return this.pullerBehaviorsByPeerId.Sum(x => x.Value.SpeedBytesPerSecond);
            }
        }

        /// <inheritdoc/>
        public void OnIbdStateChanged(bool isIbd)
        {
            lock (this.peerLock)
            {
                foreach (IBlockPullerBehavior blockPullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    blockPullerBehavior.OnIbdStateChanged(isIbd);

                this.isIbd = isIbd;
            }
        }

        /// <inheritdoc/>
        public void NewPeerTipClaimed(INetworkPeer peer, ChainedHeader newTip)
        {
            lock (this.peerLock)
            {
                int peerId = peer.Connection.Id;

                if (this.pullerBehaviorsByPeerId.TryGetValue(peerId, out IBlockPullerBehavior behavior))
                {
                    behavior.Tip = newTip;
                    this.logger.LogTrace("Tip for peer with ID {0} was changed to '{1}'.", peerId, newTip);
                }
                else
                {
                    bool supportsRequirments = this.networkPeerRequirement.Check(peer.PeerVersion, out string reason);

                    if (supportsRequirments)
                    {
                        behavior = peer.Behavior<IBlockPullerBehavior>();
                        behavior.Tip = newTip;
                        this.pullerBehaviorsByPeerId.Add(peerId, behavior);

                        this.logger.LogTrace("New peer with ID {0} and tip '{1}' was added.", peerId, newTip);
                    }
                    else
                        this.logger.LogTrace("Peer ID {0} was discarded since he doesn't support the requirements, reason: {1}", peerId, reason);
                }
            }
        }

        /// <inheritdoc/>
        public void PeerDisconnected(int peerId)
        {
            lock (this.peerLock)
            {
                this.pullerBehaviorsByPeerId.Remove(peerId);
            }

            this.ReleaseAndReassignAssignments(peerId);
        }

        /// <inheritdoc/>
        public void RequestBlocksDownload(List<ChainedHeader> headers, bool highPriority = false)
        {
            Guard.Assert(headers.Count != 0);

            lock (this.queueLock)
            {
                // Enqueue new download job.
                int jobId = this.nextJobId++;

                Queue<DownloadJob> queue = highPriority ? this.reassignedJobsQueue : this.downloadJobsQueue;

                queue.Enqueue(new DownloadJob()
                {
                    Headers = new List<ChainedHeader>(headers),
                    Id = jobId
                });

                this.logger.LogDebug("{0} blocks were requested from puller. Job ID {1} was created.", headers.Count, jobId);

                this.processQueuesSignal.Set();
            }
        }

        /// <summary>Loop that assigns download jobs to the peers.</summary>
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
                    this.logger.LogTrace("(-)[CANCELLED]");
                    return;
                }

                await this.AssignDownloadJobsAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Loop that continuously checks if peers failed to deliver important blocks in given time and penalizes them if they did.</summary>
        private async Task StallingLoopAsync()
        {
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
        }

        /// <summary>Assigns downloads from <see cref="reassignedJobsQueue"/> and <see cref="downloadJobsQueue"/> to the peers that are capable of delivering blocks.</summary>
        private async Task AssignDownloadJobsAsync()
        {
            var failedHashes = new List<uint256>();
            var newAssignments = new List<AssignedDownload>();

            lock (this.queueLock)
            {
                // First process reassign queue ignoring slots limitations.
                this.ProcessQueueLocked(this.reassignedJobsQueue, newAssignments, failedHashes);

                // Process regular queue.
                int emptySlots;
                lock (this.assignedLock)
                {
                    emptySlots = this.maxBlocksBeingDownloaded - this.assignedDownloadsByHash.Count;
                }

                int slotsThreshold = (int)(this.maxBlocksBeingDownloaded * MinEmptySlotsPercentageToStartProcessingTheQueue);

                if (emptySlots >= slotsThreshold)
                    this.ProcessQueueLocked(this.downloadJobsQueue, newAssignments, failedHashes, emptySlots);
                else
                    this.logger.LogTrace("Slots threshold is not met, queue will not be processed. There are {0} empty slots, threshold is {1}.", emptySlots, slotsThreshold);

                this.processQueuesSignal.Reset();
            }

            if (newAssignments.Count != 0)
            {
                this.logger.LogDebug("Total amount of downloads assigned in this iteration is {0}.", newAssignments.Count);
                await this.AskPeersForBlocksAsync(newAssignments).ConfigureAwait(false);
            }

            // Call callbacks with null since puller failed to deliver requested blocks.
            if (failedHashes.Count != 0)
                this.logger.LogTrace("{0} jobs partially or fully failed.", failedHashes.Count);

            foreach (uint256 failedJob in failedHashes)
            {
                // Avoid calling callbacks on shutdown.
                if (this.cancellationSource.IsCancellationRequested)
                {
                    this.logger.LogTrace("Callbacks won't be called because component is being disposed.");
                    break;
                }

                // The choice of peerId does not matter here as the callback should not attempt any validation/banning for a null block.
                this.onDownloadedCallback(failedJob, null, 0);
            }
        }

        /// <summary>Processes specified queue of download jobs.</summary>
        /// <param name="jobsQueue">Queue of download jobs to be processed.</param>
        /// <param name="newAssignments">Collection of new assignments to be populated.</param>
        /// <param name="failedHashes">List of failed hashes to be populated if some of jobs hashes can't be assigned to any peer.</param>
        /// <param name="emptySlots">Max number of assignments that can be made.</param>
        /// <remarks>Have to be locked by <see cref="queueLock"/>.</remarks>
        private void ProcessQueueLocked(Queue<DownloadJob> jobsQueue, List<AssignedDownload> newAssignments, List<uint256> failedHashes, int emptySlots = int.MaxValue)
        {
            while ((jobsQueue.Count > 0) && (emptySlots > 0))
            {
                DownloadJob jobToAssign = jobsQueue.Peek();
                int jobHeadersCount = jobToAssign.Headers.Count;

                List<AssignedDownload> assignments = this.DistributeHeadersLocked(jobToAssign, failedHashes, emptySlots);

                emptySlots -= assignments.Count;

                this.logger.LogTrace("Assigned {0} headers out of {1} for job {2}.", assignments.Count, jobHeadersCount, jobToAssign.Id);

                lock (this.assignedLock)
                {
                    foreach (AssignedDownload assignment in assignments)
                    {
                        newAssignments.Add(assignment);
                        this.AddAssignedDownloadLocked(assignment);
                    }
                }

                // Remove job from the queue if it was fully consumed.
                if (jobToAssign.Headers.Count == 0)
                    jobsQueue.Dequeue();
            }
        }

        /// <summary>
        /// Adds assigned download to <see cref="assignedDownloadsByHash"/> and helper structures <see cref="assignedDownloadsSorted"/> and <see cref="assignedHeadersByPeerId"/>.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="assignedLock"/>.</remarks>
        /// <param name="assignment">The assignment.</param>
        private void AddAssignedDownloadLocked(AssignedDownload assignment)
        {
            this.assignedDownloadsByHash.Add(assignment.Header.HashBlock, assignment);

            // Add to assignedHeadersByPeerId.
            if (!this.assignedHeadersByPeerId.TryGetValue(assignment.PeerId, out List<ChainedHeader> headersForIds))
            {
                headersForIds = new List<ChainedHeader>();
                this.assignedHeadersByPeerId.Add(assignment.PeerId, headersForIds);
            }

            headersForIds.Add(assignment.Header);

            // Add to assignedDownloadsSorted.
            LinkedListNode<AssignedDownload> lastDownload = this.assignedDownloadsSorted.Last;

            if ((lastDownload == null) || (lastDownload.Value.Header.Height <= assignment.Header.Height))
            {
                assignment.LinkedListNode = this.assignedDownloadsSorted.AddLast(assignment);
            }
            else
            {
                LinkedListNode<AssignedDownload> current = lastDownload;

                while ((current.Previous != null) && (current.Previous.Value.Header.Height > assignment.Header.Height))
                    current = current.Previous;

                assignment.LinkedListNode = this.assignedDownloadsSorted.AddBefore(current, assignment);
            }
        }

        /// <summary>
        /// Removes assigned download from <see cref="assignedDownloadsByHash"/> and helper structures <see cref="assignedDownloadsSorted"/> and <see cref="assignedHeadersByPeerId"/>.
        /// </summary>
        /// <remarks>Have to be locked by <see cref="assignedLock"/>.</remarks>
        /// <param name="assignment">Assignment that should be removed.</param>
        private void RemoveAssignedDownloadLocked(AssignedDownload assignment)
        {
            this.assignedDownloadsByHash.Remove(assignment.Header.HashBlock);

            List<ChainedHeader> headersForId = this.assignedHeadersByPeerId[assignment.PeerId];
            headersForId.Remove(assignment.Header);
            if (headersForId.Count == 0)
                this.assignedHeadersByPeerId.Remove(assignment.PeerId);

            this.assignedDownloadsSorted.Remove(assignment.LinkedListNode);
        }

        /// <summary>Asks peer behaviors in parallel to deliver blocks.</summary>
        /// <param name="assignments">Assignments given to peers.</param>
        private async Task AskPeersForBlocksAsync(List<AssignedDownload> assignments)
        {
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

                IBlockPullerBehavior peerBehavior;

                lock (this.peerLock)
                {
                    this.pullerBehaviorsByPeerId.TryGetValue(peerId, out peerBehavior);
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
        }

        /// <summary>Distributes download job's headers to peers that can provide blocks represented by those headers.</summary>
        /// <remarks>
        /// If some of the blocks from the job can't be provided by any peer those headers will be added to a <param name="failedHashes"></param>.
        /// <para>
        /// Have to be locked by <see cref="queueLock"/>.
        /// </para>
        /// <para>
        /// Node's quality score is being considered as a weight during the random distribution of the hashes to download among the nodes.
        /// </para>
        /// </remarks>
        /// <param name="downloadJob">Download job to be partially of fully consumed.</param>
        /// <param name="failedHashes">List of failed hashes which will be extended in case there is no peer to claim required hash.</param>
        /// <param name="emptySlots">Number of empty slots. This is the maximum number of assignments that can be created.</param>
        /// <returns>List of downloads that were distributed between the peers.</returns>
        private List<AssignedDownload> DistributeHeadersLocked(DownloadJob downloadJob, List<uint256> failedHashes, int emptySlots)
        {
            var newAssignments = new List<AssignedDownload>();

            HashSet<IBlockPullerBehavior> peerBehaviors;

            lock (this.peerLock)
            {
                peerBehaviors = new HashSet<IBlockPullerBehavior>(this.pullerBehaviorsByPeerId.Values);
            }

            bool jobFailed = false;

            if (peerBehaviors.Count == 0)
            {
                this.logger.LogDebug("There are no peers that can participate in download job distribution! Job ID {0} failed.", downloadJob.Id);
                jobFailed = true;
            }

            int lastSucceededIndex = -1;
            for (int index = 0; (index < downloadJob.Headers.Count) && (index < emptySlots) && !jobFailed; index++)
            {
                ChainedHeader header = downloadJob.Headers[index];

                while (!jobFailed)
                {
                    // Weighted random selection based on the peer's quality score.
                    double sumOfQualityScores = peerBehaviors.Sum(x => x.QualityScore);
                    double scoreToReachPeer = this.random.NextDouble() * sumOfQualityScores;

                    IBlockPullerBehavior selectedBehavior = peerBehaviors.First();

                    foreach (IBlockPullerBehavior peerBehavior in peerBehaviors)
                    {
                        if (peerBehavior.QualityScore >= scoreToReachPeer)
                        {
                            selectedBehavior = peerBehavior;
                            break;
                        }

                        scoreToReachPeer -= peerBehavior.QualityScore;
                    }

                    INetworkPeer attachedPeer = selectedBehavior.AttachedPeer;

                    // Behavior's tip can't be null because we only have behaviors inserted in the behaviors structure after the tip is set.
                    if ((attachedPeer != null) && (selectedBehavior.Tip.FindAncestorOrSelf(header) != null))
                    {
                        int peerId = attachedPeer.Connection.Id;

                        // Assign to this peer.
                        newAssignments.Add(new AssignedDownload()
                        {
                            PeerId = peerId,
                            JobId = downloadJob.Id,
                            AssignedTime = this.dateTimeProvider.GetUtcNow(),
                            Header = header
                        });

                        lastSucceededIndex = index;

                        this.logger.LogTrace("Block '{0}' was assigned to peer ID {1}.", header.HashBlock, peerId);
                        break;
                    }
                    else
                    {
                        // Peer doesn't claim this header.
                        peerBehaviors.Remove(selectedBehavior);

                        if (peerBehaviors.Count != 0)
                            continue;

                        jobFailed = true;
                        this.logger.LogDebug("Job {0} failed because there is no peer claiming header '{1}'.", downloadJob.Id, header);
                    }
                }
            }

            if (!jobFailed)
            {
                downloadJob.Headers.RemoveRange(0, lastSucceededIndex + 1);
            }
            else
            {
                int removeFrom = (lastSucceededIndex == -1) ? 0 : lastSucceededIndex + 1;

                IEnumerable<uint256> failed = downloadJob.Headers.GetRange(removeFrom, downloadJob.Headers.Count - removeFrom).Select(x => x.HashBlock);
                failedHashes.AddRange(failed);

                downloadJob.Headers.Clear();
            }

            return newAssignments;
        }

        /// <summary>Checks if peers failed to deliver important blocks and penalizes them if they did.</summary>
        private void CheckStalling()
        {
            int lastImportantHeight = this.chainState.ConsensusTip.Height + ImportantHeightMargin;
            this.logger.LogTrace("Blocks up to height {0} are considered to be important.", lastImportantHeight);

            var allReleasedAssignments = new List<Dictionary<int, List<ChainedHeader>>>();

            lock (this.assignedLock)
            {
                LinkedListNode<AssignedDownload> current = this.assignedDownloadsSorted.First;

                var peerIdsToReassignJobs = new HashSet<int>();

                while (current != null)
                {
                    // Since the headers in the linked list are sorted by height after we found first that is
                    // not important we can assume that the rest of them are not important.
                    if (current.Value.Header.Height > lastImportantHeight)
                        break;

                    double secondsPassed = (this.dateTimeProvider.GetUtcNow() - current.Value.AssignedTime).TotalSeconds;

                    // Peer failed to deliver important block.
                    int peerId = current.Value.PeerId;
                    current = current.Next;

                    if (secondsPassed < MaxSecondsToDeliverBlock)
                        continue;

                    // Peer already added to the collection of peers to release and reassign.
                    if (peerIdsToReassignJobs.Contains(peerId))
                        continue;

                    peerIdsToReassignJobs.Add(peerId);

                    int assignedCount = this.assignedHeadersByPeerId[peerId].Count;

                    this.logger.LogDebug("Peer {0} failed to deliver {1} blocks from which some were important.", peerId, assignedCount);

                    lock (this.peerLock)
                    {
                        IBlockPullerBehavior pullerBehavior = this.pullerBehaviorsByPeerId[peerId];
                        pullerBehavior.Penalize(secondsPassed, assignedCount);

                        this.RecalculateQualityScoreLocked(pullerBehavior, peerId);
                    }
                }

                // Release downloads for selected peers.
                foreach (int peerId in peerIdsToReassignJobs)
                {
                    Dictionary<int, List<ChainedHeader>> reassignedAssignmentsByJobId = this.ReleaseAssignmentsLocked(peerId);
                    allReleasedAssignments.Add(reassignedAssignmentsByJobId);
                }
            }

            if (allReleasedAssignments.Count > 0)
            {
                lock (this.queueLock)
                {
                    // Reassign all released jobs.
                    foreach (Dictionary<int, List<ChainedHeader>> released in allReleasedAssignments)
                        this.ReassignAssignmentsLocked(released);

                    // Trigger queue processing in case anything was reassigned.
                    this.processQueuesSignal.Set();
                }
            }
        }

        /// <inheritdoc />
        public void PushBlock(uint256 blockHash, Block block, int peerId)
        {
            AssignedDownload assignedDownload;

            lock (this.assignedLock)
            {
                if (!this.assignedDownloadsByHash.TryGetValue(blockHash, out assignedDownload))
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

                this.RemoveAssignedDownloadLocked(assignedDownload);
            }

            double deliveredInSeconds = (this.dateTimeProvider.GetUtcNow() - assignedDownload.AssignedTime).TotalSeconds;
            this.logger.LogTrace("Peer {0} delivered block '{1}' in {2} seconds.", assignedDownload.PeerId, blockHash, deliveredInSeconds);

            lock (this.peerLock)
            {
                // Add peer sample.
                if (this.pullerBehaviorsByPeerId.TryGetValue(peerId, out IBlockPullerBehavior behavior))
                {
                    behavior.AddSample(block.BlockSize.Value, deliveredInSeconds);

                    // Recalculate quality score.
                    this.RecalculateQualityScoreLocked(behavior, peerId);
                }
            }

            lock (this.queueLock)
            {
                this.averageBlockSizeBytes.AddSample(block.BlockSize.Value);

                this.RecalculateMaxBlocksBeingDownloadedLocked();

                this.processQueuesSignal.Set();
            }

            this.onDownloadedCallback(blockHash, block, peerId);
        }

        /// <summary>Recalculates quality score of a peer or all peers if given peer has the best upload speed.</summary>
        /// <remarks>This method has to be protected by <see cref="peerLock"/>.</remarks>
        /// <param name="pullerBehavior">The puller behavior of a peer which quality score should be recalculated.</param>
        /// <param name="peerId">ID of a peer which behavior is passed.</param>
        private void RecalculateQualityScoreLocked(IBlockPullerBehavior pullerBehavior, int peerId)
        {
            // Now decide if we need to recalculate quality score for all peers or just for this one.
            long bestSpeed = this.pullerBehaviorsByPeerId.Max(x => x.Value.SpeedBytesPerSecond);

            long adjustedBestSpeed = bestSpeed;
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
                foreach (IBlockPullerBehavior peerPullerBehavior in this.pullerBehaviorsByPeerId.Values)
                    peerPullerBehavior.RecalculateQualityScore(adjustedBestSpeed);
            }
        }

        /// <summary>
        /// Recalculates the maximum number of blocks that can be simultaneously downloaded based
        /// on the average blocks size and the total speed of all peers that can deliver blocks.
        /// </summary>
        /// <remarks>This object has to be protected by <see cref="queueLock" />.</remarks>
        private void RecalculateMaxBlocksBeingDownloadedLocked()
        {
            // How many blocks we can download in 1 second.
            if (this.averageBlockSizeBytes.Average > 0)
                this.maxBlocksBeingDownloaded = (int)((this.GetTotalSpeedOfAllPeersBytesPerSec() * MaxBlocksBeingDownloadedMultiplier) / this.averageBlockSizeBytes.Average);

            if (this.maxBlocksBeingDownloaded < MinimalCountOfBlocksBeingDownloaded)
                this.maxBlocksBeingDownloaded = MinimalCountOfBlocksBeingDownloaded;

            this.logger.LogTrace("Max number of blocks that can be downloaded at the same time is set to {0}.", this.maxBlocksBeingDownloaded);
        }

        /// <summary>
        /// Finds all blocks assigned to a given peer, removes assignments from <see cref="assignedDownloadsByHash"/>,
        /// adds to <see cref="reassignedJobsQueue"/> and signals the <see cref="processQueuesSignal"/>.
        /// </summary>
        /// <param name="peerId">The peer identifier.</param>
        private void ReleaseAndReassignAssignments(int peerId)
        {
            Dictionary<int, List<ChainedHeader>> headersByJobId;

            lock (this.assignedLock)
            {
                headersByJobId = this.ReleaseAssignmentsLocked(peerId);
            }

            if (headersByJobId.Count != 0)
            {
                lock (this.queueLock)
                {
                    this.ReassignAssignmentsLocked(headersByJobId);
                    this.processQueuesSignal.Set();
                }
            }
        }

        /// <summary>Finds all blocks assigned to a given peer, removes assignments from <see cref="assignedDownloadsByHash"/> and returns removed assignments.</summary>
        /// <remarks>Have to be locked by <see cref="assignedLock"/>.</remarks>
        private Dictionary<int, List<ChainedHeader>> ReleaseAssignmentsLocked(int peerId)
        {
            var headersByJobId = new Dictionary<int, List<ChainedHeader>>();

            if (this.assignedHeadersByPeerId.TryGetValue(peerId, out List<ChainedHeader> headers))
            {
                var assignmentsToRemove = new List<AssignedDownload>(headers.Count);

                foreach (ChainedHeader header in headers)
                {
                    AssignedDownload assignment = this.assignedDownloadsByHash[header.HashBlock];

                    if (!headersByJobId.TryGetValue(assignment.JobId, out List<ChainedHeader> jobHeaders))
                    {
                        jobHeaders = new List<ChainedHeader>();
                        headersByJobId.Add(assignment.JobId, jobHeaders);
                    }

                    jobHeaders.Add(assignment.Header);

                    assignmentsToRemove.Add(assignment);

                    this.logger.LogTrace("Header '{0}' for job ID {1} was released from peer ID {2}.", header, assignment.JobId, peerId);
                }

                foreach (AssignedDownload assignment in assignmentsToRemove)
                    this.RemoveAssignedDownloadLocked(assignment);
            }

            return headersByJobId;
        }

        /// <summary>Adds items from <paramref name="headersByJobId"/> to the <see cref="reassignedJobsQueue"/>.</summary>
        /// <param name="headersByJobId">Block headers mapped by job IDs.</param>
        /// <remarks>Have to be locked by <see cref="queueLock"/>.</remarks>
        private void ReassignAssignmentsLocked(Dictionary<int, List<ChainedHeader>> headersByJobId)
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

        private void AddComponentStats(StringBuilder statsBuilder)
        {
            statsBuilder.AppendLine();
            statsBuilder.AppendLine("======Block Puller======");

            lock (this.assignedLock)
            {
                int pendingBlocks = this.assignedDownloadsByHash.Count;
                statsBuilder.AppendLine($"Blocks being downloaded: {pendingBlocks}");
            }

            lock (this.queueLock)
            {
                int unassignedDownloads = 0;

                foreach (DownloadJob downloadJob in this.downloadJobsQueue)
                    unassignedDownloads += downloadJob.Headers.Count;

                statsBuilder.AppendLine($"Queueued downloads: {unassignedDownloads}");
            }

            double avgBlockSizeBytes = this.GetAverageBlockSizeBytes();
            double averageBlockSizeKb = avgBlockSizeBytes / 1024.0;
            statsBuilder.AppendLine($"Average block size: {Math.Round(averageBlockSizeKb, 2)} KB");

            double totalSpeedBytesPerSec = this.GetTotalSpeedOfAllPeersBytesPerSec();
            double totalSpeedKbPerSec = (totalSpeedBytesPerSec / 1024.0);
            statsBuilder.AppendLine($"Total download speed: {Math.Round(totalSpeedKbPerSec, 2)} KB/sec");

            double timeToDownloadBlockMs = Math.Round((avgBlockSizeBytes / totalSpeedBytesPerSec) * 1000, 2);
            statsBuilder.AppendLine($"Average time to download a block: {timeToDownloadBlockMs} ms");

            double blocksPerSec = Math.Round(totalSpeedBytesPerSec / avgBlockSizeBytes, 2);
            statsBuilder.AppendLine($"Amount of blocks node can download in 1 second: {blocksPerSec}");

            // TODO: add logging per each peer
            // peer -- quality score -- assigned blocks -- speed  (SORT BY QualityScore)
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.cancellationSource.Cancel();

            this.assignerLoop?.GetAwaiter().GetResult();
            this.stallingLoop?.GetAwaiter().GetResult();

            this.cancellationSource.Dispose();
        }
    }
}
