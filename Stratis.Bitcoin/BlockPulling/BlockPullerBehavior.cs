using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using Stratis.Bitcoin.Base;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Relation of the node to a network peer node.
    /// </summary>
    public class BlockPullerBehavior : NodeBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

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

        /// <summary>Reference to the parent block puller.</summary>
        private readonly BlockPuller puller;
        /// <summary>Reference to the parent block puller.</summary>
        public BlockPuller Puller => this.puller;

        /// <summary>Reference to a component responsible for keeping the chain up to date.</summary>
        public ChainHeadersBehavior ChainHeadersBehavior { get; private set; }

        /// <summary>Number of download tasks assigned to this peer. This is for logging purposes only.</summary>
        public int PendingDownloadsCount
        {
            get
            {
                return this.puller.GetPendingDownloadsCount(this);
            }
        }

        /// <summary>
        /// Evaluation of the past experience with this node.
        /// The higher the score, the better experience we have had with it.
        /// </summary>
        /// <seealso cref="MaxQualityScore"/>
        /// <seealso cref="MinQualityScore"/>
        /// <remarks>
        /// TODO: Race conditions touching this - https://github.com/stratisproject/StratisBitcoinFullNode/issues/247
        /// </remarks>
        public int QualityScore { get; set; }

        /// <summary>
        /// Initializes a new instance of the object with parent block puller.
        /// </summary>
        /// <param name="puller">Reference to the parent block puller.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public BlockPullerBehavior(BlockPuller puller, ILoggerFactory loggerFactory)
        {
            this.puller = puller;
            this.QualityScore = BlockPuller.MaxQualityScore / 2;
            // TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/292
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BlockPullerBehavior(this.puller, this.loggerFactory);
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
            // TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/292
            this.logger.LogTrace($"[{this.GetHashCode():x}] ({nameof(node.Peer.Endpoint)}:'{node.Peer.Endpoint}')");

            message.Message.IfPayloadIs<BlockPayload>((block) =>
            {
                uint256 blockHash = block.Object.Header.GetHash();
                this.logger.LogTrace($"[{this.GetHashCode():x}] Received block '{blockHash}', length {message.Length} bytes.");

                block.Object.Header.CacheHashes();
                this.QualityScore = Math.Min(BlockPuller.MaxQualityScore, this.QualityScore + 1);

                foreach (Transaction tx in block.Object.Transactions)
                    tx.CacheHashes();

                DownloadedBlock downloadedBlock = new DownloadedBlock()
                {
                    Block = block.Object,
                    Length = (int)message.Length,
                };

                if (this.puller.DownloadTaskFinished(this, blockHash, downloadedBlock))
                    this.puller.BlockPushed(blockHash, downloadedBlock, this.cancellationToken.Token);

                // This peer is now available for more work.
                this.AssignPendingVector();
            });

            this.logger.LogTrace($"[{this.GetHashCode():x}] (-)");
        }

        /// <summary>
        /// If there are any more blocks the node wants to download, this method assigns and starts 
        /// a new download task for a specific peer node that this behavior represents.
        /// </summary>
        internal void AssignPendingVector()
        {
            this.logger.LogTrace($"[{this.GetHashCode():x}] ()");

            Node attachedNode = this.AttachedNode;
            if (attachedNode == null || attachedNode.State != NodeState.HandShaked || !this.puller.Requirements.Check(attachedNode.PeerVersion))
            {
                this.logger.LogTrace($"[{this.GetHashCode():x}] (-)[ATTACHED_NODE]");
                return;
            }

            uint256 block = null;
            if (this.puller.AssignPendingDownloadTaskToPeer(this, out block))
                attachedNode.SendMessageAsync(new GetDataPayload(new InventoryVector(attachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), block)));

            this.logger.LogTrace($"[{this.GetHashCode():x}] (-)");
        }

        /// <summary>
        /// Sends a message to the connected peer requesting specific data.
        /// </summary>
        /// <param name="getDataPayload">Specification of the data to download - <see cref="GetDataPayload"/>.</param>
        /// <remarks>Caller is responsible to add the puller to the map if necessary.</remarks>
        internal void StartDownload(GetDataPayload getDataPayload)
        {
            this.logger.LogTrace($"[{this.GetHashCode():x}] ()");

            Node attachedNode = this.AttachedNode;
            if (attachedNode == null || attachedNode.State != NodeState.HandShaked || !this.puller.Requirements.Check(attachedNode.PeerVersion))
            {
                this.logger.LogTrace($"[{this.GetHashCode():x}] (-)[ATTACHED_NODE]");
                return;
            }

            foreach (InventoryVector inv in getDataPayload.Inventory)
                inv.Type = attachedNode.AddSupportedOptions(inv.Type);

            attachedNode.SendMessageAsync(getDataPayload);

            this.logger.LogTrace($"[{this.GetHashCode():x}] (-)");
        }

        /// <summary>
        /// Connects the puller to the node and the chain so that the puller can start its work.
        /// </summary>
        protected override void AttachCore()
        {
            this.logger.LogTrace($"[{this.GetHashCode():x}] ()");

            this.AttachedNode.MessageReceived += Node_MessageReceived;
            this.ChainHeadersBehavior = this.AttachedNode.Behaviors.Find<ChainHeadersBehavior>();
            AssignPendingVector();

            this.logger.LogTrace($"[{this.GetHashCode():x}] (-)");
        }

        /// <summary>
        /// Disconnects the puller from the node and cancels pending operations and download tasks.
        /// </summary>
        /// <remarks>
        /// TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/246
        /// </remarks>
        protected override void DetachCore()
        {
            this.logger.LogTrace($"[{this.GetHashCode():x}] ()");

            this.cancellationToken.Cancel();
            this.AttachedNode.MessageReceived -= Node_MessageReceived;
            ReleaseAll();

            this.logger.LogTrace($"[{this.GetHashCode():x}] (-)");
        }

        /// <summary>
        /// Releases all pending block download tasks from the peer.
        /// </summary>
        internal void ReleaseAll()
        {
            this.logger.LogTrace($"[{this.GetHashCode():x}] ()");

            this.puller.ReleaseAllPeerDownloadTaskAssignments(this);

            this.logger.LogTrace($"[{this.GetHashCode():x}] (-)");
        }
    }
}
