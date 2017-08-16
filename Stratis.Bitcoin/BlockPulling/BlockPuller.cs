using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Base class for pullers that download blocks from peers.
    /// <para>
    /// This must be inherited and the implementing class
    /// needs to handle taking blocks off the queue and stalling.
    /// </para>
    /// </summary>
    /// <remarks>
    /// There are 4 important objects that hold the state of the puller and that need to be kept in sync:
    /// <see cref="assignedBlockTasks"/>, <see cref="pendingInventoryVectors"/>, <see cref="downloadedBlocks"/>, 
    /// and <see cref="peersPendingDownloads"/>.
    /// <para>
    /// <see cref="downloadedBlocks"/> is a list of blocks that have been downloaded recently but not processed 
    /// by the consumer of the puller.
    /// </para>
    /// <para>
    /// When a typical consumer wants a next block from the puller, it first checks <see cref="downloadedBlocks"/>, 
    /// if the block is available (the consumer does know the header of the block it wants from the puller,
    /// if not, it simply waits until this information is available). If it is available, it is removed 
    /// from <see cref="downloadedBlocks"/> and consumed. Otherwise, the consumer checks whether this block is being 
    /// downloaded (or soon to be). If not, it asks the puller to request it from the connect network peers.
    /// <para>
    /// Besides this "on demand" way of requesting blocks from peers, the consumer also tries to keep puller 
    /// ahead of the demand, so that the blocks are downloaded some time before they are needed.
    /// </para>
    /// </para>
    /// <para>
    /// For a block to be considered as currently (or soon to be) being downloaded, its hash has to be 
    /// either in <see cref="assignedBlockTasks"/> or <see cref="pendingInventoryVectors"/>.
    /// </para>
    /// <para>
    /// When the puller is about to request blocks from the peers, it selects which of its peers will 
    /// be asked to provide which blocks. These assignments of block downloading tasks is kept inside 
    /// <see cref="assignedBlockTasks"/>. Unsatisfied requests go to <see cref="pendingInventoryVectors"/>, which happens 
    /// when the puller find out that neither of its peers can be asked for certain block. It also happens 
    /// when something goes wrong (e.g. the peer disconnects) and the downloading request to a peer is not 
    /// completed. Such requests need to be reassigned later. Note that it is possible for a peer 
    /// to be operating well, but slowly, which can cause its quality score to go down and its work 
    /// to be taken from it. However, this reassignment of the work does not mean the node is stopped 
    /// in its current task and it is still possible that it will deliver the blocks it was asked for.
    /// Such late blocks deliveries are currently ignored and wasted.
    /// </para>
    /// <para><see cref="peersPendingDownloads"/> is an inverse mapping to <see cref="assignedBlockTasks"/>. Each connected 
    /// peer node has its list of assigned tasks here and there is an equivalence between tasks in both structures.</para>
    /// </remarks>
    public abstract class BlockPuller : IBlockPuller
    {
        /// <summary>Description of a block together with its size.</summary>
        public class DownloadedBlock
        {
            /// <summary>Size of the serialized block in bytes.</summary>
            public int Length;

            /// <summary>Description of a block.</summary>
            public Block Block;
        }

        /// <summary>Number of historic samples we keep to calculate quality score stats from.</summary>
        private const int QualityScoreHistoryLength = 100;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Lock protecting access to <see cref="assignedBlockTasks"/>, <see cref="pendingInventoryVectors"/>, <see cref="downloadedBlocks"/>, 
        /// <see cref="peersPendingDownloads"/>, <see cref="peerQuality"/>, and also <see cref="BlockPullerBehavior.Disconnected"/>.
        /// </summary>
        private readonly object lockObject = new object();

        /// <summary>
        /// Hashes of blocks to be downloaded mapped by the peers that the download tasks are assigned to.
        /// </summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, BlockPullerBehavior> assignedBlockTasks;

        /// <summary>List of block header hashes that the node wants to obtain from its peers.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Queue<uint256> pendingInventoryVectors;

        /// <summary>List of unprocessed downloaded blocks mapped by their header hashes.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, DownloadedBlock> downloadedBlocks;

        /// <summary>Statistics of the recent history of network peers qualities.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly QualityScore peerQuality;

        /// <summary>Number of items in <see cref="downloadedBlocks"/>. This is for statistical purposes only.</summary>
        public int DownloadedBlocksCount
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.downloadedBlocks.Count;
                }
            }
        }

        /// <summary>Sets of download tasks representing blocks that are being downloaded mapped by peers they are assigned to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<BlockPullerBehavior, Dictionary<uint256, DownloadAssignment>> peersPendingDownloads = new Dictionary<BlockPullerBehavior, Dictionary<uint256, DownloadAssignment>>();

        /// <summary>Collection of available network peers.</summary>
        protected readonly IReadOnlyNodesCollection Nodes;

        /// <summary>Best chain that the node is aware of.</summary>
        protected readonly ConcurrentChain Chain;

        /// <summary>Random number generator.</summary>
        private Random Rand = new Random();

        /// <summary>Specification of requirements the puller has on its peer nodes to consider asking them to provide blocks.</summary>
        private readonly NodeRequirement requirements;
        /// <summary>Specification of requirements the puller has on its peer nodes to consider asking them to provide blocks.</summary>
        public virtual NodeRequirement Requirements => this.requirements;

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        /// <param name="protocolVersion">Version of the protocol that the node supports.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        protected BlockPuller(ConcurrentChain chain, IReadOnlyNodesCollection nodes, ProtocolVersion protocolVersion, ILoggerFactory loggerFactory)
        {
            this.Chain = chain;
            this.Nodes = nodes;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.downloadedBlocks = new Dictionary<uint256, DownloadedBlock>();
            this.pendingInventoryVectors = new Queue<uint256>();
            this.assignedBlockTasks = new Dictionary<uint256, BlockPullerBehavior>();
            this.peerQuality = new QualityScore(QualityScoreHistoryLength, loggerFactory);

            // Set the default requirements.
            this.requirements = new NodeRequirement
            {
                MinVersion = protocolVersion,
                RequiredServices = NodeServices.Network
            };
        }

        /// <summary>
        /// Method called when a new block is downloaded and pushed to the puller.
        /// <para>
        /// This method is to be overridden by derived classes. In the base class it only logs the event.
        /// </para>
        /// </summary>
        /// <param name="blockHash">Hash of the newly downloaded block.</param>
        /// <param name="downloadedBlock">Desciption of the newly downloaded block.</param>
        /// <param name="cancellationToken">Cancellation token to be used by derived classes that allows the caller to cancel the execution of the operation.</param>
        public virtual void BlockPushed(uint256 blockHash, DownloadedBlock downloadedBlock, CancellationToken cancellationToken)
        {
            this.logger.LogTrace($"({nameof(blockHash)}:'{blockHash}',{nameof(downloadedBlock)}.{nameof(downloadedBlock.Length)}:{downloadedBlock.Length})");
            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void InjectBlock(uint256 blockHash, DownloadedBlock downloadedBlock, CancellationToken cancellationToken)
        {
            this.logger.LogTrace($"({nameof(blockHash)}:'{blockHash}',{nameof(downloadedBlock)}.{nameof(downloadedBlock.Length)}:{downloadedBlock.Length})");

            if (this.AddDownloadedBlock(blockHash, downloadedBlock))
                this.BlockPushed(blockHash, downloadedBlock, cancellationToken);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public virtual void AskBlocks(ChainedBlock[] downloadRequests)
        {
            this.logger.LogTrace($"({nameof(downloadRequests)}:{string.Join(",", downloadRequests.Select(r => r.Height))})");

            var vectors = new Dictionary<int, InventoryVector>();
            foreach (ChainedBlock request in downloadRequests)
            {
                InventoryVector vector = new InventoryVector(InventoryType.MSG_BLOCK, request.HashBlock);
                vectors.Add(request.Height, vector);
            }
            this.DistributeDownload(vectors, downloadRequests.Min(d => d.Height));

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Constructs relations to peer nodes that meet the requirements.
        /// </summary>
        /// <returns>Array of relations to peer nodes that can be asked for blocks.</returns>
        /// <seealso cref="requirements"/>
        private BlockPullerBehavior[] GetNodeBehaviors()
        {
            return this.Nodes
                .Where(n => this.requirements.Check(n.PeerVersion))
                .SelectMany(n => n.Behaviors.OfType<BlockPullerBehavior>())
                .Where(b => b.Puller == this)
                .ToArray();
        }

        /// <summary>
        /// Reassigns the incomplete block downloading tasks among available peer nodes.
        /// <para>
        /// When something went wrong when the node wanted to download a block from a peer, 
        /// the task of obtaining the block might get released from the peer. This function 
        /// leads to assignment of the incomplete tasks to available peer nodes.
        /// </para>
        /// </summary>
        private void AssignPendingVectors()
        {
            this.logger.LogTrace("()");

            uint256[] pendingVectorsCopy;
            lock (this.lockObject)
            {
                pendingVectorsCopy = this.pendingInventoryVectors.ToArray();
                this.pendingInventoryVectors.Clear();
            }

            int minHeight = int.MaxValue;
            Dictionary<int, InventoryVector> vectors = new Dictionary<int, InventoryVector>();
            foreach (uint256 blockHash in pendingVectorsCopy)
            {
                InventoryVector vector = new InventoryVector(InventoryType.MSG_BLOCK, blockHash);

                ChainedBlock chainedBlock = this.Chain.GetBlock(vector.Hash);
                if (chainedBlock == null) // Reorg might have happened.
                    continue;

                minHeight = Math.Min(chainedBlock.Height, minHeight);
                vectors.Add(chainedBlock.Height, vector);
            }

            if (vectors.Count > 0) this.DistributeDownload(vectors, minHeight);
            else this.logger.LogTrace("No vectors assigned.");

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public void CheckBlockStatus(uint256 hash, out bool IsDownloading, out bool IsReady)
        {
            this.logger.LogTrace($"({nameof(hash)}:'{hash}')");

            lock (this.lockObject)
            {
                IsDownloading = this.assignedBlockTasks.ContainsKey(hash) || this.pendingInventoryVectors.Contains(hash);
                IsReady = this.downloadedBlocks.ContainsKey(hash);
            }

            this.logger.LogTrace($"(-):*{nameof(IsDownloading)}={IsDownloading},*{nameof(IsReady)}={IsReady}");
        }

        /// <summary>
        /// Decreases the quality score of the peer node.
        /// <para>This function is called when something goes wrong with the peer.</para>
        /// <para>If the score reaches the minimal value, the tasks assigned for the node are released.</para>
        /// </summary>
        /// <param name="chainedBlock">Block the node wanted to download, but something went wrong during the process.</param>
        protected void OnStalling(ChainedBlock chainedBlock)
        {
            this.logger.LogTrace($"({nameof(chainedBlock)}.{nameof(chainedBlock.HashBlock)}:'{chainedBlock.HashBlock}')");
            BlockPullerBehavior behavior = null;

            lock (this.lockObject)
            {
                this.assignedBlockTasks.TryGetValue(chainedBlock.HashBlock, out behavior);
            }

            if (behavior != null)
            {
                double penalty = this.peerQuality.CalculateNextBlockTimeoutQualityPenalty();
                this.logger.LogTrace($"Block '{chainedBlock.HashBlock}' assigned to '{behavior.GetHashCode():x}', penalty is {penalty}.");

                behavior.UpdateQualityScore(penalty);
                if (Math.Abs(behavior.QualityScore - QualityScore.MinScore) < 0.00001)
                {
                    behavior.ReleaseAll(false);
                    this.AssignPendingVectors();
                }
            }
            else
            {
                this.logger.LogTrace($"Block '{chainedBlock.HashBlock}' not assigned to any peer.");
                this.AssignPendingVectors();
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Schedules downloading of one or more blocks that the node is missing from one or more peer nodes.
        /// <para>
        /// Node's quality score is being considered as a weight during the random distribution 
        /// of the download tasks among the nodes.
        /// </para>
        /// <para>
        /// Nodes are only asked for blocks that they should have (according to our information 
        /// about how long their chains are).
        /// </para>
        /// </summary>
        /// <param name="vectors">List of information about blocks to download mapped by their height. Must not be empty.</param>
        /// <param name="minHeight">Minimum height of the chain that the target nodes has to have in order to be asked for one or more of the block to be downloaded from them.</param>
        private void DistributeDownload(Dictionary<int, InventoryVector> vectors, int minHeight)
        {
            this.logger.LogTrace($"({nameof(vectors)}.{nameof(vectors.Count)}:{vectors.Count},{nameof(minHeight)}:{minHeight})");

            // Count number of tasks assigned to each peer.
            BlockPullerBehavior[] nodes = this.GetNodeBehaviors();
            Dictionary<BlockPullerBehavior, int> assignedTasksCount = new Dictionary<BlockPullerBehavior, int>();
            lock (this.lockObject)
            {
                foreach (BlockPullerBehavior behavior in nodes)
                {
                    int taskCount = 0;
                    Dictionary<uint256, DownloadAssignment> peerPendingDownloads;
                    if (this.peersPendingDownloads.TryGetValue(behavior, out peerPendingDownloads))
                        taskCount = peerPendingDownloads.Keys.Count;

                    assignedTasksCount.Add(behavior, taskCount);
                }
            }

            // Prefilter available peers so that we only work with peers that can be assigned any work.
            // If there is a peer whose chain is so short that it can't provide any blocks we want, it is ignored.
            List<PullerDownloadAssignments.PeerInformation> peerInformation = new List<PullerDownloadAssignments.PeerInformation>();

            foreach (BlockPullerBehavior behavior in nodes)
            {
                int? peerHeight = behavior.ChainHeadersBehavior?.PendingTip?.Height;
                if (peerHeight >= minHeight)
                {
                    PullerDownloadAssignments.PeerInformation peerInfo = new PullerDownloadAssignments.PeerInformation()
                    {
                        QualityScore = behavior.QualityScore,
                        PeerId = behavior,
                        ChainHeight = peerHeight.Value,
                        TasksAssignedCount = assignedTasksCount[behavior]
                    };
                    peerInformation.Add(peerInfo);
                    this.logger.LogTrace($"Peer '{peerInfo.PeerId.GetHashCode():x}' available: quality {peerInfo.QualityScore}, height {peerInfo.ChainHeight}.");
                }
                else this.logger.LogTrace($"Peer '{behavior.GetHashCode():x}' filtered out: height {peerHeight}.");
            }

            // There are no available peers with long enough chains.
            if (peerInformation.Count == 0)
            {
                lock (this.lockObject)
                {
                    foreach (InventoryVector vector in vectors.Values)
                        this.pendingInventoryVectors.Enqueue(vector.Hash);
                }
                this.logger.LogTrace("(-)[NO_PEERS_LEFT]");
                return;
            }

            List<int> requestedBlockHeights = vectors.Keys.ToList();
            Dictionary<PullerDownloadAssignments.PeerInformation, List<int>> blocksAssignedToPeers = PullerDownloadAssignments.AssignBlocksToPeers(requestedBlockHeights, peerInformation);

            // Go through the assignments and start download tasks.
            foreach (KeyValuePair<PullerDownloadAssignments.PeerInformation, List<int>> kvp in blocksAssignedToPeers)
            {
                PullerDownloadAssignments.PeerInformation peer = kvp.Key;
                List<int> blockHeightsToDownload = kvp.Value;

                GetDataPayload getDataPayload = new GetDataPayload();
                BlockPullerBehavior peerBehavior = (BlockPullerBehavior)peer.PeerId;

                // Create GetDataPayload from the list of block heights this peer has been assigned.
                bool peerDisconnected = false;
                foreach (int blockHeight in blockHeightsToDownload)
                {
                    InventoryVector inventoryVector = vectors[blockHeight];
                    if (this.AssignDownloadTaskToPeer(peerBehavior, inventoryVector.Hash, out peerDisconnected))
                    {
                        this.logger.LogTrace($"Block '{inventoryVector.Hash}/{blockHeight}' assigned to peer '{peerBehavior.GetHashCode():x}'");
                        getDataPayload.Inventory.Add(inventoryVector);
                    }
                    else if (peerDisconnected)
                    {
                        // The peer was disconnected recently, we need to make sure that the blocks assigned to it go back to the pending list.
                        // This is done below.
                        this.logger.LogTrace($"Peer '{peerBehavior.GetHashCode():x} has been disconnected.'");
                        break;
                    }
                    // else This block has been assigned to someone else already, no action required.
                }

                if (!peerDisconnected)
                {
                    // If this node was assigned at least one download task, start the task.
                    if (getDataPayload.Inventory.Count > 0)
                        peerBehavior.StartDownload(getDataPayload);
                }
                else
                {
                    // Return blocks that were supposed to be assigned to the disconnected peer back to the pending list.
                    lock (this.lockObject)
                    {
                        foreach (int blockHeight in blockHeightsToDownload)
                        {
                            InventoryVector inventoryVector = vectors[blockHeight];
                            this.pendingInventoryVectors.Enqueue(inventoryVector.Hash);
                        }
                    }
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Assigns a pending download task to a specific peer.
        /// </summary>
        /// <param name="peer">Peer to be assigned the new task.</param>
        /// <param name="blockHash">If the function succeeds, this is filled with the hash of the block that will be requested from <paramref name="peer"/>.</param>
        /// <returns>
        /// <c>true</c> if a download task was assigned to the peer, <c>false</c> otherwise, 
        /// which indicates that there was no pending task, or that the peer is disconnected and should not be assigned any more work.
        /// </returns>
        internal bool AssignPendingDownloadTaskToPeer(BlockPullerBehavior peer, out uint256 blockHash)
        {
            this.logger.LogTrace($"({nameof(peer)}:'{peer.GetHashCode():x}')");
            blockHash = null;

            lock (this.lockObject)
            {
                if (!peer.Disconnected && (this.pendingInventoryVectors.Count > 0))
                {
                    blockHash = this.pendingInventoryVectors.Dequeue();
                    if (this.assignedBlockTasks.TryAdd(blockHash, peer))
                        this.AddPeerPendingDownloadLocked(peer, blockHash);
                }
            }

            bool res = blockHash != null;

            this.logger.LogTrace($"(-):{res},*{nameof(blockHash)}='{blockHash}'");
            return res;
        }

        /// <summary>
        /// Assigns a download task to a specific peer.
        /// </summary>
        /// <param name="peer">Peer to be assigned the new task.</param>
        /// <param name="blockHash">Hash of the block to download from <paramref name="peer"/>.</param>
        /// <param name="peerDisconnected">If the function fails, this is set to <c>true</c> if the peer was marked as disconnected and thus unable to be assigned any more work.</param>
        /// <returns>
        /// <c>true</c> if the block was assigned to the peer, <c>false</c> in case the block has already been assigned to someone, 
        /// or if the peer is disconnected and should not be assigned any more work.
        /// </returns>
        internal bool AssignDownloadTaskToPeer(BlockPullerBehavior peer, uint256 blockHash, out bool peerDisconnected)
        {
            this.logger.LogTrace($"({nameof(peer)}:'{peer.GetHashCode():x}',{nameof(blockHash)}:'{blockHash}')");

            bool res = false;
            lock (this.lockObject)
            {
                peerDisconnected = peer.Disconnected;
                if (!peerDisconnected && this.assignedBlockTasks.TryAdd(blockHash, peer))
                {
                    this.AddPeerPendingDownloadLocked(peer, blockHash);
                    res = true;
                }
            }

            this.logger.LogTrace($"(-):{res}");
            return res;
        }

        /// <summary>
        /// Releases the block downloading task from the peer it has been assigned to 
        /// and returns the block to the list of blocks the node wants to download.
        /// </summary>
        /// <param name="peerPendingDownloads">List of pending downloads tasks of the peer.</param>
        /// <param name="blockHash">Hash of the block which task should be released.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> if the block was not assigned to be downloaded by any peer.</returns>
        /// <remarks>The caller of this method is responsible for holding <see cref="lockObject"/>.</remarks>
        private bool ReleaseDownloadTaskAssignmentLocked(Dictionary<uint256, DownloadAssignment> peerPendingDownloads, uint256 blockHash)
        {
            this.logger.LogTrace($"({nameof(peerPendingDownloads)}.{nameof(peerPendingDownloads.Count)}:{peerPendingDownloads.Count},{nameof(blockHash)}:{blockHash})");

            bool res = false;
            if (this.assignedBlockTasks.Remove(blockHash) && peerPendingDownloads.Remove(blockHash))
            {
                this.pendingInventoryVectors.Enqueue(blockHash);
                res = true;
            }

            this.logger.LogTrace($"(-):{res}");
            return res;
        }

        /// <summary>
        /// Releases all pending block download tasks assigned to a peer.
        /// </summary>
        /// <param name="peer">Peer to have all its pending download task released.</param>
        /// <param name="disconnectPeer">If set to <c>true</c> the peer is considered as disconnected and should be prevented from being assigned additional work.</param>
        /// <exception cref="InvalidOperationException">Thrown in case of data inconsistency between synchronized structures, which should never happen.</exception>
        internal void ReleaseAllPeerDownloadTaskAssignments(BlockPullerBehavior peer, bool disconnectPeer)
        {
            this.logger.LogTrace($"({nameof(peer)}:'{peer.GetHashCode():x}',{nameof(disconnectPeer)}:{disconnectPeer})");

            lock (this.lockObject)
            {
                // Prevent the peer to get any more work from now on if it was disconnected.
                if (disconnectPeer) peer.Disconnected = true;

                Dictionary<uint256, DownloadAssignment> peerPendingDownloads;
                if (this.peersPendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                {
                    this.logger.LogTrace($"Releasing {peerPendingDownloads.Count} pending downloads of peer '{peer.GetHashCode():x}'.");

                    // Make a fresh copy of items in peerPendingDownloads to avoid modification of the collection.
                    foreach (uint256 blockHash in peerPendingDownloads.Keys.ToList())
                    {
                        if (!this.ReleaseDownloadTaskAssignmentLocked(peerPendingDownloads, blockHash))
                        {
                            this.logger.LogCritical("Data structures inconsistency, please notify the devs.");
                            throw new InvalidOperationException("Data structures inconsistency, please notify the devs.");
                        }
                    }

                    this.peersPendingDownloads.Remove(peer);
                }
            }

            this.logger.LogTrace($"(-)");
        }

        /// <summary>
        /// When a peer downloads a block, it notifies the puller about the block by calling this method.
        /// <para>
        /// The downloaded task is removed from the list of pending downloads
        /// and it is also removed from the <see cref="assignedBlockTasks"/> - i.e. the task is no longer assigned to the peer.
        /// And finally, it is added to the list of downloaded blocks, provided that the block is not present there already.
        /// </para>
        /// </summary>
        /// <param name="peer">Peer that finished the download task.</param>
        /// <param name="blockHash">Hash of the downloaded block.</param>
        /// <param name="downloadedBlock">Description of the downloaded block.</param>
        /// <returns>
        /// <c>true</c> if the download task for the block was assigned to <paramref name="peer"/> 
        /// and the task was removed and added to the list of downloaded blocks. 
        /// <c>false</c> if the downloaded block has been assigned to another peer
        /// or if the block was already on the list of downloaded blocks.
        /// </returns>
        internal bool DownloadTaskFinished(BlockPullerBehavior peer, uint256 blockHash, DownloadedBlock downloadedBlock)
        {
            this.logger.LogTrace($"({nameof(peer)}:'{peer.GetHashCode():x}',{nameof(blockHash)}:'{blockHash}',{nameof(downloadedBlock)}.{nameof(downloadedBlock.Length)}:{downloadedBlock.Length})");

            bool error = false;
            bool res = false;

            double peerQualityAdjustment = 0;

            lock (this.lockObject)
            {
                BlockPullerBehavior peerAssigned;
                if (this.assignedBlockTasks.TryGetValue(blockHash, out peerAssigned))
                {
                    Dictionary<uint256, DownloadAssignment> peerPendingDownloads;
                    if (this.peersPendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    {
                        if (peer == peerAssigned)
                        {
                            DownloadAssignment downloadTask = null;
                            peerPendingDownloads.TryGetValue(blockHash, out downloadTask);

                            if (this.assignedBlockTasks.Remove(blockHash) && peerPendingDownloads.Remove(blockHash))
                            {
                                // Task was assigned to this peer and was removed.
                                if (this.downloadedBlocks.TryAdd(blockHash, downloadedBlock))
                                {
                                    long blockDownloadTime = downloadTask.Finish();
                                    this.peerQuality.AddSample(peer, blockDownloadTime, downloadedBlock.Length);
                                    peerQualityAdjustment = this.peerQuality.CalculateQualityAdjustment(blockDownloadTime, downloadedBlock.Length);

                                    this.logger.LogTrace($"Block '{blockHash}' size '{downloadedBlock.Length}' downloaded by peer '{peer.GetHashCode():x}' in {blockDownloadTime} ms, peer's score will be adjusted by {peerQualityAdjustment}.");

                                    res = true;
                                }
                                else this.logger.LogTrace($"Block '{blockHash}' already present on the list of downloaded blocks.");
                            }
                            else
                            {
                                // Task was assigned to this peer but the data are inconsistent.
                                error = true;
                            }
                        }
                        else
                        {
                            // Before this peer provided the block, it has been assigned to other peer, which is OK.
                            this.logger.LogTrace($"Incoming block '{blockHash}' is assigned to peer '{peerAssigned.GetHashCode():x}', not to '{peer.GetHashCode():x}'.");
                        }
                    }
                    else
                    {
                        // Peer's pending downloads were probably released, which is OK.
                        this.logger.LogTrace($"Peer '{peer.GetHashCode():x}' has no assignments.");
                    }
                }
                else
                {
                    // The task was probably assigned to other peer and that task completed before this peer provided the block, which is OK.
                    this.logger.LogTrace($"Incoming block '{blockHash}' is not pending.");
                }
            }

            if (error)
            {
                this.logger.LogCritical("Data structures inconsistency, please notify the devs.");

                // TODO: This exception is going to be silently discarded by Node_MessageReceived.
                throw new InvalidOperationException("Data structures inconsistency, please notify the devs.");
            }

            if (res) peer.UpdateQualityScore(peerQualityAdjustment);

            this.logger.LogTrace($"(-):{res}");
            return res;
        }

        /// <summary>
        /// Retrieves a downloaded block from list of downloaded blocks, but does not remove the block from the list.
        /// </summary>
        /// <param name="blockHash">Hash of the block to obtain.</param>
        /// <returns>Downloaded block or null if block with the given hash is not on the list.</returns>
        protected DownloadedBlock GetDownloadedBlock(uint256 blockHash)
        {
            this.logger.LogTrace($"({nameof(blockHash)}:'{blockHash}')");

            DownloadedBlock res = null;
            lock (this.lockObject)
            {
                res = this.downloadedBlocks.TryGet(blockHash);
            }

            this.logger.LogTrace($"(-):'{res}'");
            return res;
        }

        /// <summary>
        /// Adds a downloaded block to the list of downloaded blocks.
        /// <para>
        /// If a block with the same hash already existed in the list, 
        /// it is not replaced with the new one, but the function does not fail.
        /// </para>
        /// </summary>
        /// <param name="blockHash">Hash of the block to add.</param>
        /// <param name="downloadedBlock">Downloaded block to add.</param>
        /// <returns><c>true</c> if the block was added to the list of downloaded blocks, <c>false</c> if the block was already present.</returns>
        private bool AddDownloadedBlock(uint256 blockHash, DownloadedBlock downloadedBlock)
        {
            bool res = false;

            lock (this.lockObject)
            {
                res = this.downloadedBlocks.TryAdd(blockHash, downloadedBlock);
            }

            return res;
        }

        /// <summary>
        /// Get and remove a downloaded block from the list of downloaded blocks.
        /// </summary>
        /// <param name="blockHash">Hash of the block to retrieve.</param>
        /// <param name="downloadedBlock">If the function succeeds, this is filled with the downloaded block, which hash is <paramref name="blockHash"/>.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> if the block with the given hash was not in the list.</returns>
        protected bool TryRemoveDownloadedBlock(uint256 blockHash, out DownloadedBlock downloadedBlock)
        {
            this.logger.LogTrace($"({nameof(blockHash)}:'{blockHash}')");

            bool res = false;

            lock (this.lockObject)
            {
                if (this.downloadedBlocks.TryGetValue(blockHash, out downloadedBlock))
                    res = this.downloadedBlocks.Remove(blockHash);
            }

            if (res) this.logger.LogTrace($"(-):{res},*{nameof(downloadedBlock)}.{nameof(downloadedBlock.Length)}:{downloadedBlock.Length}");
            else this.logger.LogTrace($"(-):{res}");
            return res;
        }

        /// <summary>
        /// Obtains a number of tasks assigned to a peer.
        /// </summary>
        /// <param name="peer">Peer to get number of assigned tasks for.</param>
        /// <returns>Number of tasks assigned to <paramref name="peer"/>.</returns>
        internal int GetPendingDownloadsCount(BlockPullerBehavior peer)
        {
            int res = 0;
            lock (this.lockObject)
            {
                Dictionary<uint256, DownloadAssignment> peerPendingDownloads;
                if (this.peersPendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    res = peerPendingDownloads.Count;
            }
            return res;
        }

        /// <summary>
        /// Adds download task to the peer's list of pending download tasks.
        /// </summary>
        /// <param name="peer">Peer to add task to.</param>
        /// <param name="blockHash">Hash of the block being assigned to <paramref name="peer"/> for download.</param>
        /// <remarks>The caller of this method is responsible for holding <see cref="lockObject"/>.</remarks>
        private void AddPeerPendingDownloadLocked(BlockPullerBehavior peer, uint256 blockHash)
        {
            this.logger.LogTrace($"({nameof(peer)}:'{peer.GetHashCode():x}',{nameof(blockHash)}:'{blockHash}')");

            Dictionary<uint256, DownloadAssignment> peerPendingDownloads;
            if (!this.peersPendingDownloads.TryGetValue(peer, out peerPendingDownloads))
            {
                peerPendingDownloads = new Dictionary<uint256, DownloadAssignment>();
                this.peersPendingDownloads.Add(peer, peerPendingDownloads);
            }

            DownloadAssignment downloadTask = new DownloadAssignment(blockHash);
            peerPendingDownloads.Add(blockHash, downloadTask);
            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Checks if the puller behavior is currently responsible for downloading specific block.
        /// </summary>
        /// <param name="peer">Peer's behavior to check the assignment for.</param>
        /// <param name="blockHash">Hash of the block.</param>
        /// <returns><c>true</c> if the <paramref name="peer"/> is currently responsible for downloading block with hash <paramref name="blockHash"/>.</returns>
        public bool CheckBlockTaskAssignment(BlockPullerBehavior peer, uint256 blockHash)
        {
            this.logger.LogTrace($"({nameof(peer)}:'{peer.GetHashCode():x}',{nameof(blockHash)}:'{blockHash}')");

            bool res = false;
            lock (this.lockObject)
            {
                Dictionary<uint256, DownloadAssignment> peerPendingDownloads;
                if (this.peersPendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    res = peerPendingDownloads.ContainsKey(blockHash);
            }

            this.logger.LogTrace($"(-):{res}");
            return res;
        }
    }
}