﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using static Stratis.Bitcoin.BlockPulling.BlockPuller;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Relation of the node's puller to a network peer node.
    /// </summary>
    public interface IBlockPullerBehavior
    {
        /// <summary>
        /// Evaluation of the past experience with this node.
        /// The higher the score, the better experience we have had with it.
        /// </summary>
        /// <seealso cref="QualityScore.MaxScore"/>
        /// <seealso cref="QualityScore.MinScore"/>
        double QualityScore { get; }
    }

    /// <inheritdoc cref="IBlockPullerBehavior"/>
    public class BlockPullerBehavior : NetworkPeerBehavior, IBlockPullerBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

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
        public CancellationTokenSource CancellationTokenSource
        {
            get { return this.cancellationToken; }
        }

        /// <summary>Reference to the parent block puller.</summary>
        private readonly BlockPuller puller;

        /// <summary>Reference to the parent block puller.</summary>
        public BlockPuller Puller
        {
            get { return this.puller; }
        }

        /// <summary>Reference to a component responsible for keeping the chain up to date.</summary>
        public ChainHeadersBehavior ChainHeadersBehavior { get; private set; }

        /// <summary>Set to <c>true</c> when the puller behavior is disconnected, so that the associated network peer can get no more download tasks.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="BlockPuller.lockObject"/>.</remarks>
        internal bool Disconnected { get; set; }

        /// <summary>Number of download tasks assigned to this peer. This is for logging purposes only.</summary>
        public int PendingDownloadsCount
        {
            get
            {
                return this.puller.GetPendingDownloadsCount(this);
            }
        }

        /// <summary>Lock protecting write access to <see cref="QualityScore"/>.</summary>
        private readonly object qualityScoreLock = new object();

        /// <inheritdoc />
        /// <remarks>Write access to this object has to be protected by <see cref="qualityScoreLock"/>.</remarks>
        public double QualityScore { get; private set; }

        /// <summary>
        /// Initializes a new instance of the object with parent block puller.
        /// </summary>
        /// <param name="puller">Reference to the parent block puller.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        public BlockPullerBehavior(BlockPuller puller, ILoggerFactory loggerFactory)
        {
            this.puller = puller;
            this.QualityScore = BlockPulling.QualityScore.MaxScore / 3;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BlockPullerBehavior(this.puller, this.loggerFactory);
        }

        /// <summary>
        /// Event handler that is called when a message is received from the attached peer.
        /// <para>
        /// This handler modifies internal state when an information about a block is received.
        /// </para>
        /// </summary>
        /// <param name="peer">Peer that sent us the message.</param>
        /// <param name="message">Received message.</param>
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (message.Message.Payload is BlockPayload block)
            {
                // There are two pullers for each peer connection and each is having its own puller behavior.
                // Both these behaviors get notification from the node when it receives a message,
                // even if the origin of the message was from the other puller behavior.
                // Therefore we first make a quick check whether this puller behavior was the one
                // who should deal with this block.
                uint256 blockHash = block.Obj.Header.GetHash();
                if (this.puller.CheckBlockTaskAssignment(this, blockHash))
                {
                    this.logger.LogTrace("Received block '{0}', length {1} bytes.", blockHash, message.Length);

                    block.Obj.Header.PrecomputeHash();
                    foreach (Transaction tx in block.Obj.Transactions)
                        tx.CacheHashes();

                    var downloadedBlock = new DownloadedBlock
                    {
                        Block = block.Obj,
                        Length = (int)message.Length,
                        Peer = peer.RemoteSocketEndpoint
                    };

                    if (this.puller.DownloadTaskFinished(this, blockHash, downloadedBlock))
                        this.puller.BlockPushed(blockHash, downloadedBlock, this.cancellationToken.Token);

                    // This peer is now available for more work.
                    await this.AssignPendingVectorAsync().ConfigureAwait(false);
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// If there are any more blocks the node wants to download, this method assigns and starts
        /// a new download task for a specific peer that this behavior represents.
        /// </summary>
        private async Task AssignPendingVectorAsync()
        {
            this.logger.LogTrace("()");

            INetworkPeer attachedNode = this.AttachedPeer;
            if ((attachedNode == null) || (attachedNode.State != NetworkPeerState.HandShaked) || !this.puller.Requirements.Check(attachedNode.PeerVersion))
            {
                this.logger.LogTrace("(-)[ATTACHED_NODE]");
                return;
            }

            uint256 block = null;
            if (this.puller.AssignPendingDownloadTaskToPeer(this, out block))
            {
                try
                {
                    var getDataPayload = new GetDataPayload(new InventoryVector(attachedNode.AddSupportedOptions(InventoryType.MSG_BLOCK), block));
                    await attachedNode.SendMessageAsync(getDataPayload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Sends a message to the connected peer requesting specific data.
        /// </summary>
        /// <param name="getDataPayload">Specification of the data to download - <see cref="GetDataPayload"/>.</param>
        /// <returns><c>true</c> if the message was successfully sent to the peer, <c>false</c> if the peer got disconnected.</returns>
        /// <remarks>Caller is responsible to add the puller to the map if necessary.</remarks>
        internal async Task<bool> StartDownloadAsync(GetDataPayload getDataPayload)
        {
            this.logger.LogTrace("()");

            INetworkPeer attachedNode = this.AttachedPeer;
            if ((attachedNode == null) || (attachedNode.State != NetworkPeerState.HandShaked) || !this.puller.Requirements.Check(attachedNode.PeerVersion))
            {
                this.logger.LogTrace("(-)[ATTACHED_PEER]:false");
                return false;
            }

            foreach (InventoryVector inv in getDataPayload.Inventory)
                inv.Type = attachedNode.AddSupportedOptions(inv.Type);

            try
            {
                await attachedNode.SendMessageAsync(getDataPayload).ConfigureAwait(false);

                // In case job is assigned to a peer with low quality score-
                // give it enough score so the job is not reassigned right away.
                this.UpdateQualityScore(BlockPulling.QualityScore.MaxScore / 10);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogTrace("(-)[CANCELLED]:false");
                return false;
            }

            this.logger.LogTrace("(-):true");
            return true;
        }

        /// <summary>
        /// Connects the puller to the node and the chain so that the puller can start its work.
        /// </summary>
        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
            this.ChainHeadersBehavior = this.AttachedPeer.Behaviors.Find<ChainHeadersBehavior>();
            this.AssignPendingVectorAsync().GetAwaiter().GetResult();

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Disconnects the puller from the node and cancels pending operations and download tasks.
        /// </summary>
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.cancellationToken.Cancel();
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            this.ReleaseAll(true);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Releases all pending block download tasks from the peer.
        /// </summary>
        /// <param name="peerDisconnected">If set to <c>true</c> the peer is considered as disconnected and should be prevented from being assigned additional work.</param>
        internal void ReleaseAll(bool peerDisconnected)
        {
            this.logger.LogTrace("({0}:{1})", nameof(peerDisconnected), peerDisconnected);

            this.puller.ReleaseAllPeerDownloadTaskAssignments(this, peerDisconnected);

            this.logger.LogTrace("(-)");
        }

        /// <summary>
        /// Adjusts the quality score of the peer.
        /// </summary>
        /// <param name="scoreAdjustment">Adjustment to make to the quality score of the peer.</param>
        internal void UpdateQualityScore(double scoreAdjustment)
        {
            this.logger.LogTrace("({0}:{1})", nameof(scoreAdjustment), scoreAdjustment);

            lock (this.qualityScoreLock)
            {
                this.QualityScore += scoreAdjustment;
                if (this.QualityScore > BlockPulling.QualityScore.MaxScore) this.QualityScore = BlockPulling.QualityScore.MaxScore;
                if (this.QualityScore < BlockPulling.QualityScore.MinScore) this.QualityScore = BlockPulling.QualityScore.MinScore;
            }

            this.logger.LogTrace("(-):{0}={1}", nameof(this.QualityScore), this.QualityScore);
        }
    }
}
