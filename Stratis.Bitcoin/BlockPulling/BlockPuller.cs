using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Collections.Concurrent;
using NBitcoin.Protocol.Behaviors;
using System.Threading;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Connection;
using Microsoft.Extensions.Logging;

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
    /// <see cref="map"/>, <see cref="pendingInventoryVectors"/>, <see cref="downloadedBlocks"/>, and <see cref="pendingDownloads"/>.
    /// <para>
    /// <see cref="downloadedBlocks"/> is a list of blocks that have been downloaded recently but not processed 
    /// by the consumer of the puller.
    /// </para>
    /// <para>
    /// When a typical consumer wants a next block from the puller, it first checks <see cref="downloadedBlocks"/>, 
    /// if the block is available (the consumer does know the header of the block it wants from the puller,
    /// if not, it simply waits until this information is available). If it is available, it is removed 
    /// from DownloadedBlocks and consumed. Otherwise, the consumer checks whether this block is being 
    /// downloaded (or soon to be). If not, it asks the puller to request it from the connect network peers.
    /// <para>
    /// Besides this "on demand" way of requesting blocks from peers, the consumer also tries to keep puller 
    /// ahead of the demand, so that the blocks are downloaded some time before they are needed.
    /// </para>
    /// </para>
    /// <para>
    /// For a block to be considered as currently (or soon to be) being downloaded, its hash has to be 
    /// either in <see cref="map"/> or <see cref="pendingInventoryVectors"/>.
    /// </para>
    /// <para>
    /// When the puller is about to request blocks from the peers, it selects which of its peers will 
    /// be asked to provide which blocks. These assignments of block downloading tasks is kept inside 
    /// <see cref="map"/>. Unsatified requests go to <see cref="pendingInventoryVectors"/>, which happens 
    /// when the puller find out that neither of its peers can be asked for certain block. It also happens 
    /// when something goes wrong (e.g. the peer disconnects) and the downloading request to a peer is not 
    /// completed. Such requests need to be reassigned later.
    /// </para>
    /// <para><see cref="pendingDownloads"/> is an inverse mapping to <see cref="map"/>. Each connected 
    /// peer node has its list of assigned tasks here and there is an equivalence between tasks in both structures.</para>
    /// </remarks>
    public abstract class BlockPuller : IBlockPuller
    {
        /// <summary>Maximal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const int MaxQualityScore = 150;

        /// <summary>Minimal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const int MinQualityScore = 1;

        /// <summary>Instance logger.</summary>
        protected readonly ILogger logger;

        /// <summary>Lock protecting access to <see cref="map"/>, <see cref="pendingInventoryVectors"/>, <see cref="downloadedBlocks"/>, and <see cref="pendingDownloads"/></summary>
        private readonly object lockObject = new object();

        /// <summary>
        /// List of relations to peer nodes mapped by block header hashes that the peers are requested to provide.
        /// </summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, BlockPullerBehavior> map;

        /// <summary>List of block header hashes that the node wants to obtain from its peers.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Queue<uint256> pendingInventoryVectors;

        /// <summary>List of unprocessed downloaded blocks mapped by their header hashes.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, DownloadedBlock> downloadedBlocks;

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

        /// <summary>Hash set holding set of block header hashes that are being downloaded, mapped by connected peer's behavior.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<BlockPullerBehavior, HashSet<uint256>> pendingDownloads = new Dictionary<BlockPullerBehavior, HashSet<uint256>>();

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

        /// <summary>Description of a block together with its size.</summary>
        public class DownloadedBlock
        {
            /// <summary>Size of the serialized block in bytes.</summary>
            public int Length;

            /// <summary>Description of a block.</summary>
            public Block Block;
        }

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
            this.map = new Dictionary<uint256, BlockPullerBehavior>();

            // set the default requirements
            this.requirements = new NodeRequirement
            {
                MinVersion = protocolVersion,
                RequiredServices = NodeServices.Network
            };
        }

        /// <inheritdoc />
        public virtual void PushBlock(int length, Block block, CancellationToken token)
        {
            uint256 hash = block.Header.GetHash();

            DownloadedBlock downloadedBlock = new DownloadedBlock()
            {
                Block = block,
                Length = length,
            };

            lock (this.lockObject)
            {
                this.downloadedBlocks.TryAdd(hash, downloadedBlock);
            }
        }

        /// <inheritdoc />
        public virtual void AskBlocks(ChainedBlock[] downloadRequests)
        {
            BlockPullerBehavior[] nodes = GetNodeBehaviors();

            Dictionary<int, InventoryVector> vectors = new Dictionary<int, InventoryVector>();
            foreach (ChainedBlock request in downloadRequests)
            {
                InventoryVector vector = new InventoryVector(InventoryType.MSG_BLOCK, request.HashBlock);
                vectors.Add(request.Height, vector);
            }
            DistributeDownload(vectors, nodes, downloadRequests.Min(d => d.Height));
        }

        /// <summary>
        /// Constructs relations to peer nodes that meet the requirements.
        /// </summary>
        /// <returns>Array of relations to peer nodes that can be asked for blocks.</returns>
        /// <seealso cref="requirements"/>
        /// <remarks>TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/246</remarks>
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

            BlockPullerBehavior[] innernodes = GetNodeBehaviors();
            if (innernodes.Length == 0)
                return;

            if (this.pendingInventoryVectors.Count == 0)
                return;

            int minHeight = int.MaxValue;
            Dictionary<int, InventoryVector> vectors = new Dictionary<int, InventoryVector>();

            uint256 blockHash;
            while (true)
            {
                lock (this.lockObject)
                {
                    if (this.pendingInventoryVectors.Count == 0)
                        break;

                    blockHash = this.pendingInventoryVectors.Dequeue();
                }

                InventoryVector vector = new InventoryVector(InventoryType.MSG_BLOCK, blockHash);

                ChainedBlock chainedBlock = this.Chain.GetBlock(vector.Hash);
                if (chainedBlock == null) // reorg might have happened.
                    continue;

                minHeight = Math.Min(chainedBlock.Height, minHeight);
                vectors.Add(chainedBlock.Height, vector);
            }

            if (vectors.Count > 0)
                DistributeDownload(vectors, innernodes, minHeight);
        }

        /// <inheritdoc />
        public bool IsDownloading(uint256 hash)
        {
            bool res = false;
            lock (this.lockObject)
            {
                res = this.map.ContainsKey(hash) || this.pendingInventoryVectors.Contains(hash);
            }
            return res;
        }

        /// <summary>
        /// Decreases the quality score of the peer node.
        /// <para>This function is called when something goes wrong with the peer.</para>
        /// <para>If the score reaches the minimal value, the tasks assigned for the node are released.</para>
        /// </summary>
        /// <param name="chainedBlock">Block the node wanted to download, but something went wrong during the process.</param>
        protected void OnStalling(ChainedBlock chainedBlock)
        {
            BlockPullerBehavior behavior = null;

            lock (this.lockObject)
            {
                this.map.TryGetValue(chainedBlock.HashBlock, out behavior);
            }

            if (behavior != null)
            {
                behavior.QualityScore = Math.Max(MinQualityScore, behavior.QualityScore - 1);
                if (behavior.QualityScore == MinQualityScore)
                {
                    // TODO: this does not necessarily mean the node is slow
                    // the best way is to check the nodes download speed, how
                    // many kb/s the node for the node download speed.
                    behavior.ReleaseAll();
                    AssignPendingVectors();
                }
            }
            else
            {
                AssignPendingVectors();
            }
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
        /// <param name="vectors">Information about blocks to download.</param>
        /// <param name="innerNodes">Available nodes to distribute download tasks among.</param>
        /// <param name="minHeight">Minimum height of the chain that the target nodes has to have in order to be asked for one or more of the block to be downloaded from them.</param>
        private void DistributeDownload(Dictionary<int, InventoryVector> vectors, BlockPullerBehavior[] innerNodes, int minHeight)
        {
            if (vectors.Count == 0)
                return;

            // Prefilter available peers so that we only work with peers that can be assigned any work.
            // If there is a peer whose chain is so short that it can't provide any blocks we want, it is ignored.
            List<PullerDownloadAssignments.PeerInformation> peerInformation = new List<PullerDownloadAssignments.PeerInformation>();

            foreach (BlockPullerBehavior behavior in innerNodes)
            {
                if (behavior.ChainHeadersBehavior?.PendingTip?.Height >= minHeight)
                {
                    PullerDownloadAssignments.PeerInformation peerInfo = new PullerDownloadAssignments.PeerInformation()
                    {
                        QualityScore = behavior.QualityScore,
                        PeerId = behavior,
                        ChainHeight = behavior.ChainHeadersBehavior.PendingTip.Height
                    };
                    peerInformation.Add(peerInfo);
                }
            }

            // There are no available peers with long enough chains.
            if (peerInformation.Count == 0)
            {
                lock (this.lockObject)
                {
                    foreach (InventoryVector vector in vectors.Values)
                        this.pendingInventoryVectors.Enqueue(vector.Hash);
                }
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
                foreach (int blockHeight in blockHeightsToDownload)
                {
                    InventoryVector inventoryVector = vectors[blockHeight];
                    if (AssignDownloadTaskToPeer(peerBehavior, inventoryVector.Hash))
                        getDataPayload.Inventory.Add(inventoryVector);
                }

                // If this node was assigned at least one download task, start the task.
                if (getDataPayload.Inventory.Count > 0)
                    peerBehavior.StartDownload(getDataPayload);
            }
        }

        /// <summary>
        /// Assigns a pending download task to a specific peer.
        /// </summary>
        /// <param name="peer">Peer to be assigned the new task.</param>
        /// <param name="blockHash">If the function succeeds, this is filled with the hash of the block that will be requested from <paramref name="peer"/>.</param>
        /// <returns>
        /// <c>true</c> if a download task was assigned to the peer, <c>false</c> otherwise, 
        /// which indicates that there was no pending task.
        /// </returns>
        internal bool AssignPendingDownloadTaskToPeer(BlockPullerBehavior peer, out uint256 blockHash)
        {
            blockHash = null;

            lock (this.lockObject)
            {
                if (this.pendingInventoryVectors.Count > 0)
                {
                    blockHash = this.pendingInventoryVectors.Dequeue();
                    this.map.Add(blockHash, peer);

                    HashSet<uint256> peerPendingDownloads;
                    if (!this.pendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    {
                        peerPendingDownloads = new HashSet<uint256>();
                        this.pendingDownloads.Add(peer, peerPendingDownloads);
                    }

                    peerPendingDownloads.Add(blockHash);
                }
            }

            bool res = blockHash != null;
            return res;
        }

        /// <summary>
        /// Assigns a download task to a specific peer.
        /// </summary>
        /// <param name="peer">Peer to be assigned the new task.</param>
        /// <param name="blockHash">Hash of the block to download from <paramref name="peer"/>.</param>
        /// <remarks>The caller of this method is responsible for holding <see cref="lockObject"/>.</remarks>
        /// <returns><c>true</c> if the block was assigned to the peer, <c>false</c> in case the block has already been assigned to someone.</returns>
        internal bool AssignDownloadTaskToPeer(BlockPullerBehavior peer, uint256 blockHash)
        {
            bool res = false;
            lock (this.lockObject)
            {
                res = AssignDownloadTaskToPeerLocked(peer, blockHash);
            }

            return res;
        }

        /// <summary>
        /// Assigns a download task to a specific peer.
        /// </summary>
        /// <param name="peer">Peer to be assigned the new task.</param>
        /// <param name="blockHash">Hash of the block to download from <paramref name="peer"/>.</param>
        /// <remarks>The caller of this method is responsible for holding <see cref="lockObject"/>.</remarks>
        /// <returns><c>true</c> if the block was assigned to the peer, <c>false</c> in case the block has already been assigned to someone.</returns>
        private bool AssignDownloadTaskToPeerLocked(BlockPullerBehavior peer, uint256 blockHash)
        {
            bool res = false;

            if (this.map.TryAdd(blockHash, peer))
            {
                HashSet<uint256> peerPendingDownloads;
                if (!this.pendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                {
                    peerPendingDownloads = new HashSet<uint256>();
                    this.pendingDownloads.Add(peer, peerPendingDownloads);
                }

                peerPendingDownloads.Add(blockHash);
                res = true;
            }

            return res;
        }

        /// <summary>
        /// Releases the block downloading task from the peer it has been assigned to 
        /// and returns the block to the list of blocks the node wants to download.
        /// </summary>
        /// <param name="peer">Peer to release the download task assignment for.</param>
        /// <param name="blockHash">Hash of the block which task should be released.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> if the block was not assigned to be downloaded by any peer.</returns>
        /// <exception cref="InvalidOperationException">Thrown in case of data inconsistency between synchronized structures, which should never happen.</exception>
        internal bool ReleaseDownloadTaskAssignment(BlockPullerBehavior peer, uint256 blockHash)
        {
            bool res = false;
            lock (this.lockObject)
            {
                HashSet<uint256> peerPendingDownloads;
                if (this.pendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    res = ReleaseDownloadTaskAssignmentLocked(peerPendingDownloads, blockHash);
            }

            if (!res) throw new InvalidOperationException("Data structures inconsistency, please notify the devs");

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
        private bool ReleaseDownloadTaskAssignmentLocked(HashSet<uint256> peerPendingDownloads, uint256 blockHash)
        {
            bool res = false;

            if (this.map.Remove(blockHash) && peerPendingDownloads.Remove(blockHash))
            {
                this.pendingInventoryVectors.Enqueue(blockHash);
                res = true;
            }

            return res;
        }

        /// <summary>
        /// Releases all pending block download tasks assigned to a peer.
        /// </summary>
        /// <param name="peer">Peer to have all its pending download task released.</param>
        /// <exception cref="InvalidOperationException">Thrown in case of data inconsistency between synchronized structures, which should never happen.</exception>
        internal void ReleaseAllPeerDownloadTaskAssignments(BlockPullerBehavior peer)
        {
            lock (this.lockObject)
            {
                HashSet<uint256> peerPendingDownloads;
                if (this.pendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                {
                    // Make a fresh copy of items in peerPendingDownloads to avoid modification of the collection.
                    foreach (uint256 blockHash in peerPendingDownloads.ToList())
                    {
                        if (!ReleaseDownloadTaskAssignmentLocked(peerPendingDownloads, blockHash))
                        {
                            this.logger.LogCritical("ReleaseAllPeerDownloadTaskAssignments(): Data structures inconsistency, please notify the devs");
                            throw new InvalidOperationException("Data structures inconsistency, please notify the devs");
                        }
                    }

                    this.pendingDownloads.Remove(peer);
                }
            }
        }

        /// <summary>
        /// When a peer downloads a block, it notifies the puller about the block by calling this method.
        /// <para>
        /// The downloaded task is removed from the list of pending downloads
        /// and it is also removed from the map - i.e. the task is no longer assigned to the peer.
        /// </para>
        /// </summary>
        /// <param name="peer">Peer that finished the download task.</param>
        /// <param name="blockHash">Hash of the downloaded block.</param>
        /// <returns>
        /// <c>true</c> if the download task for the block was assigned to <paramref name="peer"/> 
        /// and the task was removed. <c>false</c> if the downloaded block has been assigned to other peer.</returns>
        internal bool DownloadTaskFinished(BlockPullerBehavior peer, uint256 blockHash)
        {
            bool error = false;
            bool res = false;

            lock (this.lockObject)
            {
                BlockPullerBehavior peerAssigned;
                if (this.map.TryGetValue(blockHash, out peerAssigned))
                {
                    HashSet<uint256> peerPendingDownloads;
                    if (this.pendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    {
                        if (peer == peerAssigned)
                        {
                            if (this.map.Remove(blockHash) && peerPendingDownloads.Remove(blockHash))
                            {
                                // Task was assigned to this peer and was removed.
                                res = true;
                            }
                            else
                            {
                                // Task was assigned to this peer but the data are inconsistent.
                                error = true;
                            }
                        }
                        // else Before this peer provided the block, it has been assigned to other peer, which is OK.
                    }
                    // else Peer's pending downloads were probably released, which is OK.
                }
                // else The task was probably assigned to other peer and that task completed before this peer provided the block, which is OK.
            }

            if (error)
            {
                this.logger.LogCritical("Data structures inconsistency, please notify the devs");
                
                // TODO: This exception is going to be silently discarded by Node_MessageReceived.
                throw new InvalidOperationException("Data structures inconsistency, please notify the devs");
            }
            return res;
        }

        /// <summary>
        /// Retrieves a downloaded block from list of downloaded blocks, but does not remove the block from the list.
        /// </summary>
        /// <param name="blockHash">Hash of the block to obtain.</param>
        /// <returns>Downloaded block or null if block with the given hash is not on the list.</returns>
        protected DownloadedBlock GetDownloadedBlock(uint256 blockHash)
        {
            DownloadedBlock res = null;
            lock (this.lockObject)
            {
                res = this.downloadedBlocks.TryGet(blockHash);
            }
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
        protected void AddDownloadedBlock(uint256 blockHash, DownloadedBlock downloadedBlock)
        {
            lock (this.lockObject)
            {
                this.downloadedBlocks.TryAdd(blockHash, downloadedBlock);
            }
        }

        /// <summary>
        /// Gets and remove a downloaded block from the list of downloaded blocks.
        /// </summary>
        /// <param name="blockHash">Hash of the block to retrieve.</param>
        /// <param name="downloadedBlock">If the function succeeds, this is filled with the downloaded block, which hash is <paramref name="blockHash"/>.</param>
        /// <returns><c>true</c> if the function succeeds, <c>false</c> if the block with the given hash was not in the list.</returns>
        protected bool TryRemoveDownloadedBlock(uint256 blockHash, out DownloadedBlock downloadedBlock)
        {
            bool res = false;

            lock (this.lockObject)
            {
                if (this.downloadedBlocks.TryGetValue(blockHash, out downloadedBlock))
                    res = this.downloadedBlocks.Remove(blockHash);
            }

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
                HashSet<uint256> peerPendingDownloads;
                if (this.pendingDownloads.TryGetValue(peer, out peerPendingDownloads))
                    res = peerPendingDownloads.Count;
            }
            return res;
        }
    }
}
