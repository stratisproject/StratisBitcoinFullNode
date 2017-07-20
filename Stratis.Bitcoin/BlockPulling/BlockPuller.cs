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

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Base class for pullers that download blocks from peers.
    /// <para>
    /// This must be inherited and the implementing class
    /// needs to handle taking blocks off the queue and stalling.
    /// </para>
    /// </summary>
    public abstract class BlockPuller : IBlockPuller
    {
        /// <summary>Maximal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const int MaxQualityScore = 150;

        /// <summary>Minimal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const int MinQualityScore = 1;

        /// <summary>List of relations to peer nodes mapped by block header hashes that the peers are requested to provide.</summary>
        private readonly ConcurrentDictionary<uint256, BlockPullerBehavior> map;

        /// <summary>List of block header hashes that the node wants to obtain from its peers.</summary>
        private readonly ConcurrentBag<uint256> pendingInventoryVectors;

        /// <summary>List of unprocessed downloaded blocks mapped by their header hashes.</summary>
        protected readonly ConcurrentDictionary<uint256, DownloadedBlock> DownloadedBlocks;

        /// <summary>Collection of available network peers.</summary>
        protected readonly IReadOnlyNodesCollection Nodes;

        /// <summary>Best chain that the node is aware of.</summary>
        protected readonly ConcurrentChain Chain;

        /// <summary>Random number generator.</summary>
        private Random Rand = new Random();

        /// <summary>Specification of requirements the puller has on its peer nodes to consider asking them to provide blocks.</summary>
        private readonly NodeRequirement requirements;
        /// <summary>Specification of requirements the puller has on its peer nodes to consider asking them to provide blocks.</summary>
        protected virtual NodeRequirement Requirements => this.requirements;

        /// <summary>Description of a block together with its size.</summary>
        public class DownloadedBlock
        {
            /// <summary>Size of the serialized block in bytes.</summary>
            public int Length;

            /// <summary>Description of a block.</summary>
            public Block Block;
        }

        /// <summary>
        /// Relation of the node to a network peer node.
        /// </summary>
        public class BlockPullerBehavior : NodeBehavior
        {
            /// <summary>
            /// Token that allows cancellation of async tasks. 
            /// It is used during component shutdown.
            /// </summary>
            private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();
            /// <summary>
            /// Token that allows cancellation of async tasks. 
            /// It is used during component shutdown.
            /// </summary>
            public CancellationTokenSource CancellationTokenSource => this.cancellationToken;

            /// <summary>Hash set holding set of block header hashes that are being downloaded.</summary>
            /// <remarks>Implemented as dictionary due to missing ConcurrentHashSet implementation.</remarks>
            private readonly ConcurrentDictionary<uint256, uint256> pendingDownloads = new ConcurrentDictionary<uint256, uint256>();
            /// <summary>Set of block header hashes that are being downloaded.</summary>
            public ICollection<uint256> PendingDownloads => this.pendingDownloads.Values;

            /// <summary>Reference to the parent block puller.</summary>
            private readonly BlockPuller puller;

            /// <summary>Reference to the parent block puller.</summary>
            public BlockPuller Puller => this.puller;

            /// <summary>Reference to a component responsible for keeping the chain up to date.</summary>
            public ChainHeadersBehavior ChainHeadersBehavior { get; private set; }


            /// <summary>
            /// Initializes a new instance of the object with parent block puller.
            /// </summary>
            /// <param name="puller">Reference to the parent block puller.</param>
            public BlockPullerBehavior(BlockPuller puller)
            {
                this.puller = puller;
                this.QualityScore = MaxQualityScore / 2;
            }

            /// <inheritdoc />
            public override object Clone()
            {
                return new BlockPullerBehavior(this.puller);
            }

            /// <summary>
            /// Evaluation of the past experience with this node.
            /// The higher the score, the better experience we have had with it.
            /// </summary>
            /// <seealso cref="MaxQualityScore"/>
            /// <seealso cref="MinQualityScore"/>
            public int QualityScore
            {
                get; set;
            }


            /// <summary>
            /// Event handler that is called when the attached node receives a network message.
            /// <para>
            /// This handler modifies internal state when an information about a block is received.
            /// </para>
            /// </summary>
            /// <param name="node">Node that received the message.</param>
            /// <param name="message">Received message.</param>
            private void Node_MessageReceived(Node node, IncomingMessage message)
            {
                message.Message.IfPayloadIs<BlockPayload>((block) =>
                {
                    block.Object.Header.CacheHashes();
                    this.QualityScore = Math.Min(MaxQualityScore, this.QualityScore + 1);
                    uint256 unused;
                    if (this.pendingDownloads.TryRemove(block.Object.Header.GetHash(), out unused))
                    {
                        BlockPullerBehavior unused2;
                        if (this.puller.map.TryRemove(block.Object.Header.GetHash(), out unused2))
                        {
                            foreach (Transaction tx in block.Object.Transactions)
                                tx.CacheHashes();
                            this.puller.PushBlock((int)message.Length, block.Object, this.cancellationToken.Token);
                            this.AssignPendingVector();
                        }
                        else
                        {
                            throw new InvalidOperationException("This should not happen, please notify the devs");
                        }
                    }
                });
            }

            /// <summary>
            /// If there are any more blocks the node wants to download, this method assigns and starts 
            /// a new download task for a specific peer node that this behavior represents.
            /// </summary>
            internal void AssignPendingVector()
            {
                if (this.AttachedNode == null || this.AttachedNode.State != NodeState.HandShaked || !this.puller.requirements.Check(this.AttachedNode.PeerVersion))
                    return;
                uint256 block;
                if (this.puller.pendingInventoryVectors.TryTake(out block))
                {
                    StartDownload(block);
                }
            }

            /// <summary>
            /// Sends a message to the connected peer requesting downloading of a block.
            /// </summary>
            /// <param name="block">Hash of the block header to download.</param>
            internal void StartDownload(uint256 block)
            {
                if (this.puller.map.TryAdd(block, this))
                {
                    this.pendingDownloads.TryAdd(block, block);
                    this.AttachedNode.SendMessageAsync(new GetDataPayload(new InventoryVector(this.AttachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), block)));
                }
            }

            /// <summary>
            /// Sends a message to the connected peer requesting specific data.
            /// </summary>
            /// <param name="getDataPayload">Specification of the data to download - <see cref="GetDataPayload"/>.</param>
            /// <remarks>Caller is responsible to add the puller to the map if necessary.</remarks>
            internal void StartDownload(GetDataPayload getDataPayload)
            {
                foreach (InventoryVector inv in getDataPayload.Inventory)
                {
                    inv.Type = this.AttachedNode.AddSupportedOptions(inv.Type);
                    this.pendingDownloads.TryAdd(inv.Hash, inv.Hash);
                }
                this.AttachedNode.SendMessageAsync(getDataPayload);
            }

            /// <summary>
            /// Connects the puller to the node and the chain so that the puller can start its work.
            /// </summary>
            protected override void AttachCore()
            {
                this.AttachedNode.MessageReceived += Node_MessageReceived;
                this.ChainHeadersBehavior = this.AttachedNode.Behaviors.Find<ChainHeadersBehavior>();
                AssignPendingVector();
            }

            /// <summary>
            /// Disconnects the puller from the node and cancels pending operations and download tasks.
            /// </summary>
            protected override void DetachCore()
            {
                this.cancellationToken.Cancel();
                this.AttachedNode.MessageReceived -= Node_MessageReceived;
                foreach (KeyValuePair<uint256, BlockPullerBehavior> download in this.puller.map.ToArray())
                {
                    if (download.Value == this)
                    {
                        Release(download.Key);
                    }
                }
            }

            /// <summary>
            /// Releases the block downloading task from the puller and returns the block to the list of blocks the node wants to download.
            /// </summary>
            /// <param name="blockHash">Hash of the block which task should be released.</param>
            internal void Release(uint256 blockHash)
            {
                BlockPullerBehavior unused;
                uint256 unused2;
                if (this.puller.map.TryRemove(blockHash, out unused))
                {
                    this.pendingDownloads.TryRemove(blockHash, out unused2);
                    this.puller.pendingInventoryVectors.Add(blockHash);
                }
            }

            /// <summary>
            /// Releases all pending block download tasks from the puller.
            /// </summary>
            public void ReleaseAll()
            {
                foreach (uint256 h in this.PendingDownloads.ToArray())
                {
                    Release(h);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        /// <param name="protocolVersion">Version of the protocol that the node supports.</param>
        protected BlockPuller(ConcurrentChain chain, IReadOnlyNodesCollection nodes, ProtocolVersion protocolVersion)
        {
            this.Chain = chain;
            this.Nodes = nodes;
            this.DownloadedBlocks = new ConcurrentDictionary<uint256, DownloadedBlock>();
            this.pendingInventoryVectors = new ConcurrentBag<uint256>();
            this.map = new ConcurrentDictionary<uint256, BlockPullerBehavior>();

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
            this.DownloadedBlocks.TryAdd(hash, new DownloadedBlock { Block = block, Length = length });
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

            List<InventoryVector> inventoryVectors = new List<InventoryVector>();
            uint256 result;
            while (this.pendingInventoryVectors.TryTake(out result))
            {
                inventoryVectors.Add(new InventoryVector(InventoryType.MSG_BLOCK, result));
            }

            Dictionary<int, InventoryVector> vectors = new Dictionary<int, InventoryVector>();
            var minHeight = int.MaxValue;
            foreach (InventoryVector vector in inventoryVectors.ToArray())
            {
                ChainedBlock chainedBlock = this.Chain.GetBlock(vector.Hash);
                if (chainedBlock == null) // reorg might have happened.
                {
                    inventoryVectors.Remove(vector);
                    continue;
                }
                minHeight = Math.Min(chainedBlock.Height, minHeight);
                vectors.Add(chainedBlock.Height, vector);
            }
            if (inventoryVectors.Any())
            {
                DistributeDownload(vectors, innernodes, minHeight);
            }
        }

        /// <inheritdoc />
        public bool IsDownloading(uint256 hash)
        {
            return this.map.ContainsKey(hash) || this.pendingInventoryVectors.Contains(hash);
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
            if (this.map.TryGetValue(chainedBlock.HashBlock, out behavior))
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
            List<DownloadAssignmentStrategy.PeerInformation> peerInformation = new List<DownloadAssignmentStrategy.PeerInformation>();

            foreach (BlockPullerBehavior behavior in innerNodes)
            {
                if (behavior.ChainHeadersBehavior?.PendingTip?.Height >= minHeight)
                {
                    DownloadAssignmentStrategy.PeerInformation peerInfo = new DownloadAssignmentStrategy.PeerInformation()
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
                foreach (InventoryVector v in vectors.Values)
                    this.pendingInventoryVectors.Add(v.Hash);
                return;
            }

            List<int> requestedBlockHeights = vectors.Keys.ToList();
            Dictionary<DownloadAssignmentStrategy.PeerInformation, List<int>> blocksAssignedToPeers = DownloadAssignmentStrategy.AssignBlocksToPeers(requestedBlockHeights, peerInformation);

            // Go through the assignments and start download tasks.
            foreach (KeyValuePair<DownloadAssignmentStrategy.PeerInformation, List<int>> kvp in blocksAssignedToPeers)
            {
                DownloadAssignmentStrategy.PeerInformation peer = kvp.Key;
                List<int> blockHeightsToDownload = kvp.Value;

                GetDataPayload getDataPayload = new GetDataPayload();
                BlockPullerBehavior peerBehavior = (BlockPullerBehavior)peer.PeerId;

                // Create GetDataPayload from the list of block heights this peer has been assigned.
                foreach (int blockHeight in blockHeightsToDownload)
                {
                    InventoryVector inventoryVector = vectors[blockHeight];
                    if (this.map.TryAdd(inventoryVector.Hash, peerBehavior))
                        getDataPayload.Inventory.Add(inventoryVector);
                }

                // If this node was assigned at least one download task, start the task.
                if (getDataPayload.Inventory.Count > 0)
                    peerBehavior.StartDownload(getDataPayload);
            }
        }
    }
}
