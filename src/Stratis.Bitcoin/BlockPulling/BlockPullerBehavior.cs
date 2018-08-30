using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Relation of the node's puller to a network peer node.
    /// Keeps all peer-related values that <see cref="BlockPuller"/> needs to know about a peer.
    /// </summary>
    /// <remarks>The component is not thread safe and it is supposed to be protected by the caller.</remarks>
    public interface IBlockPullerBehavior : INetworkPeerBehavior
    {
        /// <summary>Relative quality score of a peer.</summary>
        /// <remarks>It's a value from <see cref="BlockPullerBehavior.MinQualityScore"/> to <see cref="BlockPullerBehavior.MaxQualityScore"/>.</remarks>
        double QualityScore { get; }

        /// <summary>How many blocks peer can deliver in one second.</summary>
        double BlockDeliveryRate { get; }

        /// <summary>Tip claimed by peer.</summary>
        ChainedHeader Tip { get; set; }

        /// <summary>Adds peer performance sample that is used to estimate peer's qualities.</summary>
        /// <param name="delaySeconds">Time in seconds it took peer to deliver a block.</param>
        void AddSample(double delaySeconds);

        /// <summary>Recalculates the quality score for this peer.</summary>
        /// <param name="bestBlockDeliveryRate">Highest <see cref="BlockDeliveryRate"/> between all peers.</param>
        void RecalculateQualityScore(double bestBlockDeliveryRate);

        /// <summary>Requests blocks from this peer.</summary>
        /// <param name="hashes">Hashes of blocks that should be asked to be delivered.</param>
        /// <exception cref="OperationCanceledException">Thrown in case peer is in the wrong state or TCP connection was closed during sending a message.</exception>
        Task RequestBlocksAsync(List<uint256> hashes);
    }

    /// <inheritdoc cref="IBlockPullerBehavior"/>
    public class BlockPullerBehavior : NetworkPeerBehavior, IBlockPullerBehavior
    {
        public const double MinQualityScore = 0.01;
        public const double MaxQualityScore = 1.0;

        /// <summary>Default quality score used when there are no samples to calculate the quality score.</summary>
        public const double SamplelessQualityScore = 0.3;

        /// <summary>Maximum number of samples that can be used for quality score calculation.</summary>
        internal const int SamplesCount = 100;

        /// <summary>By how much times <see cref="BlockDeliveryRate"/> can increase per recalculation.</summary>
        private const double MaxBlockDeliveryRateIncrease = 2;

        /// <summary>By how much times <see cref="BlockDeliveryRate"/> can decrease per recalculation.</summary>
        private const double MaxBlockDeliveryRateDecrease = 0.5;

        /// <inheritdoc />
        public double QualityScore { get; private set; }

        /// <inheritdoc />
        public double BlockDeliveryRate { get; private set; }

        /// <inheritdoc />
        public ChainedHeader Tip { get; set; }

        /// <summary>The average delay in seconds between asking this peer for a block and it being downloaded.</summary>
        internal readonly AverageCalculator averageDelaySeconds;

        /// <inheritdoc cref="ILoggerFactory"/>
        private readonly ILoggerFactory loggerFactory;

        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        /// <inheritdoc cref="IBlockPuller"/>
        private readonly IBlockPuller blockPuller;

        /// <inheritdoc cref="IInitialBlockDownloadState"/>
        private readonly IInitialBlockDownloadState ibdState;

        public BlockPullerBehavior(IBlockPuller blockPuller, IInitialBlockDownloadState ibdState, ILoggerFactory loggerFactory)
        {
            this.ibdState = ibdState;
            this.QualityScore = SamplelessQualityScore;

            this.averageDelaySeconds = new AverageCalculator(SamplesCount);
            this.BlockDeliveryRate = 1.0;

            this.blockPuller = blockPuller;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc/>
        public void AddSample(double delaySeconds)
        {
            this.logger.LogTrace("({0}:{1})", nameof(delaySeconds), delaySeconds);

            this.averageDelaySeconds.AddSample(delaySeconds);

            double multiplyer = 1.0 / this.averageDelaySeconds.Average;

            if (multiplyer > MaxBlockDeliveryRateIncrease)
                multiplyer = MaxBlockDeliveryRateIncrease;

            if (multiplyer < MaxBlockDeliveryRateDecrease)
                multiplyer = MaxBlockDeliveryRateDecrease;

            this.BlockDeliveryRate *= multiplyer;

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc/>
        public void RecalculateQualityScore(double bestBlockDeliveryRate)
        {
            this.logger.LogTrace("({0}:{1})", nameof(bestBlockDeliveryRate), bestBlockDeliveryRate);

            this.QualityScore = this.BlockDeliveryRate / bestBlockDeliveryRate;

            if (this.QualityScore < MinQualityScore)
                this.QualityScore = MinQualityScore;

            if (this.QualityScore > MaxQualityScore)
                this.QualityScore = MaxQualityScore;

            this.logger.LogTrace("Quality score was set to {0}.", this.QualityScore);
            this.logger.LogTrace("(-)");
        }

        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (message.Message.Payload is BlockPayload block)
            {
                uint256 blockHash = block.Obj.GetHash();

                this.logger.LogTrace("Block '{0}' delivered.", blockHash);
                this.blockPuller.PushBlock(blockHash, block.Obj, peer.Connection.Id);
            }

            this.logger.LogTrace("(-)");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task RequestBlocksAsync(List<uint256> hashes)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(hashes), nameof(hashes.Count), hashes.Count);

            var getDataPayload = new GetDataPayload();

            INetworkPeer peer = this.AttachedPeer;

            if (peer == null)
            {
                this.logger.LogTrace("(-)[PEER_DETACHED]");
                throw new OperationCanceledException("Peer is detached already!");
            }

            foreach (uint256 uint256 in hashes)
            {
                var vector = new InventoryVector(InventoryType.MSG_BLOCK, uint256);
                vector.Type = peer.AddSupportedOptions(vector.Type);

                getDataPayload.Inventory.Add(vector);
            }

            if (peer.State != NetworkPeerState.HandShaked)
            {
                this.logger.LogTrace("(-)[ATTACHED_PEER]");
                throw new OperationCanceledException("Peer is in the wrong state!");
            }

            await peer.SendMessageAsync(getDataPayload).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BlockPullerBehavior(this.blockPuller, this.ibdState, this.loggerFactory);
        }

        /// <inheritdoc />
        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }
    }
}
