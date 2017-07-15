using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Collections.Concurrent;
using NBitcoin.Protocol.Behaviors;
using System.Threading;
using Stratis.Bitcoin.Connection;

namespace Stratis.Bitcoin.BlockPulling
{
    // A puller that download blocks from peers
    // this must be inherited and the implementing class
    // needs to handle taking blocks off the queue and stalling
    public abstract class BlockPuller : IBlockPuller
    {
        public const int MaxQualityScore = 150;
        public const int MinQualityScore = 1;

        private readonly ConcurrentDictionary<uint256, BlockPullerBehavior> map;
        private readonly ConcurrentBag<uint256> pendingInventoryVectors;
        protected readonly ConcurrentDictionary<uint256, DownloadedBlock> DownloadedBlocks;

        protected readonly IReadOnlyNodesCollection Nodes;
        protected readonly ConcurrentChain Chain;
        private Random _Rand = new Random();

        private readonly NodeRequirement requirements;
        protected virtual NodeRequirement Requirements => this.requirements;

        public class DownloadedBlock
        {
            public int Length;
            public Block Block;
        }

        public class BlockPullerBehavior : NodeBehavior
        {
            private readonly CancellationTokenSource cancellationToken = new CancellationTokenSource();
            public CancellationTokenSource CancellationTokenSource => this.cancellationToken;

            private readonly ConcurrentDictionary<uint256, uint256> pendingDownloads = new ConcurrentDictionary<uint256, uint256>();
            public ICollection<uint256> PendingDownloads => this.pendingDownloads.Values;

            private readonly BlockPuller puller;
            public BlockStore.ChainBehavior ChainBehavior { get; private set; }

            public BlockPuller Puller => this.puller;

            public BlockPullerBehavior(BlockPuller puller)
            {
                this.puller = puller;
                this.QualityScore = 75;
            }
            public override object Clone()
            {
                return new BlockPullerBehavior(this.puller);
            }

            public int QualityScore
            {
                get; set;
            }

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

            internal void StartDownload(uint256 block)
            {
                if (this.puller.map.TryAdd(block, this))
                {
                    this.pendingDownloads.TryAdd(block, block);
                    this.AttachedNode.SendMessageAsync(new GetDataPayload(new InventoryVector(this.AttachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), block)));
                }
            }

            //Caller should add to the puller map
            internal void StartDownload(GetDataPayload getDataPayload)
            {
                foreach (InventoryVector inv in getDataPayload.Inventory)
                {
                    inv.Type = this.AttachedNode.AddSupportedOptions(inv.Type);
                    this.pendingDownloads.TryAdd(inv.Hash, inv.Hash);
                }
                this.AttachedNode.SendMessageAsync(getDataPayload);                
            }

            protected override void AttachCore()
            {
                this.AttachedNode.MessageReceived += Node_MessageReceived;
                this.ChainBehavior = this.AttachedNode.Behaviors.Find<BlockStore.ChainBehavior>();
                AssignPendingVector();
            }

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

            public void ReleaseAll()
            {
                foreach (uint256 h in this.PendingDownloads.ToArray())
                {
                    Release(h);
                }
            }

        }

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

        /// <summary>
        /// Psuh a block using the cancellation token belonging to the behaviour that pushed the block
        /// </summary>
        public virtual void PushBlock(int length, Block block, CancellationToken token)
        {
            uint256 hash = block.Header.GetHash();
            this.DownloadedBlocks.TryAdd(hash, new DownloadedBlock { Block = block, Length = length });
        }

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

        private BlockPullerBehavior[] GetNodeBehaviors()
        {
            return this.Nodes
                .Where(n => this.requirements.Check(n.PeerVersion))
                .SelectMany(n => n.Behaviors.OfType<BlockPullerBehavior>())
                .Where(b => b.Puller == this)
                .ToArray();
        }

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
            var minheight = int.MaxValue;
            foreach (InventoryVector vector in inventoryVectors.ToArray())
            {
                ChainedBlock chainedBlock = this.Chain.GetBlock(vector.Hash);
                if (chainedBlock == null) // reorg might have happened.
                {
                    inventoryVectors.Remove(vector);
                    continue;
                }
                minheight = Math.Min(chainedBlock.Height, minheight);
                vectors.Add(chainedBlock.Height, vector);
            }
            if (inventoryVectors.Any())
            {
                DistributeDownload(vectors, innernodes, minheight);
            }
        }

        public bool IsDownloading(uint256 hash)
        {
            return this.map.ContainsKey(hash) || this.pendingInventoryVectors.Contains(hash);
        }

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

        private void DistributeDownload(Dictionary<int, InventoryVector> vectors, BlockPullerBehavior[] innernodes, int minHight)
        {
            if (vectors.Count == 0)
                return;

            List<int> requestedBlockHeights = new List<int>();
            List<DownloadAssignmentStrategy.PeerInformation> peerInformation = new List<DownloadAssignmentStrategy.PeerInformation>();

            foreach (BlockPullerBehavior behavior in innernodes)
            {
                if (behavior.ChainBehavior?.PendingTip?.Height >= minHight)
                {
                    DownloadAssignmentStrategy.PeerInformation peerInfo = new DownloadAssignmentStrategy.PeerInformation()
                    {
                        QualityScore = behavior.QualityScore,
                        PeerId = behavior,
                        ChainHeight = behavior.ChainBehavior.PendingTip.Height
                    };
                    peerInformation.Add(peerInfo);
                }
            }

            Dictionary<DownloadAssignmentStrategy.PeerInformation, List<int>> assignBlocksToPeers = DownloadAssignmentStrategy.AssignBlocksToPeers(requestedBlockHeights, peerInformation);

            // There are no available peers with long enough chains.
            if (peerInformation.Count == 0)
            {
                foreach (InventoryVector v in vectors.Values)
                    this.pendingInventoryVectors.Add(v.Hash);
                return;
            }

            // Go through the assignments and start download tasks.
            foreach (KeyValuePair<DownloadAssignmentStrategy.PeerInformation, List<int>> kvp in assignBlocksToPeers)
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
