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
using TracerAttributes;

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

        /// <summary>Upload speed of a peer in bytes per second.</summary>
        long SpeedBytesPerSecond { get; }

        /// <summary>Tip claimed by peer.</summary>
        ChainedHeader Tip { get; set; }

        /// <summary>
        /// Adds peer performance sample that is used to estimate peer's qualities.
        /// </summary>
        /// <param name="blockSizeBytes">Block size in bytes.</param>
        /// <param name="delaySinceRequestedSeconds">Time in seconds it took peer to deliver a block since it was requested.</param>
        void AddSample(long blockSizeBytes, double delaySinceRequestedSeconds);

        /// <summary>Applies a penalty to a peer for not delivering a block.</summary>
        /// <param name="delaySeconds">Time in which peer didn't deliver assigned blocks.</param>
        /// <param name="notDeliveredBlocksCount">Number of blocks peer failed to deliver.</param>
        void Penalize(double delaySeconds, int notDeliveredBlocksCount);

        /// <summary>Called when IBD state changed.</summary>
        void OnIbdStateChanged(bool isIbd);

        /// <summary>Recalculates the quality score for this peer.</summary>
        /// <param name="bestSpeedBytesPerSecond">Speed in bytes per second that is considered to be the maximum speed.</param>
        void RecalculateQualityScore(long bestSpeedBytesPerSecond);

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

        /// <summary>Maximum number of samples that can be used for quality score calculation when node is in IBD.</summary>
        internal const int IbdSamplesCount = 200;

        /// <summary>Maximum number of samples that can be used for quality score calculation when node is not in IBD.</summary>
        internal const int NormalSamplesCount = 10;

        /// <summary>The maximum percentage of samples that can be used when peer is being penalized for not delivering blocks.</summary>
        /// <remarks><c>1</c> is 100%, <c>0</c> is 0%.</remarks>
        internal const double MaxSamplesPercentageToPenalize = 0.1;

        /// <summary>Limitation on the peer speed estimation.</summary>
        private const int MaxSpeedBytesPerSecond = 1024 * 1024 * 1024;

        /// <inheritdoc />
        public double QualityScore { get; private set; }

        /// <inheritdoc />
        public long SpeedBytesPerSecond { get; private set; }

        /// <inheritdoc />
        public ChainedHeader Tip { get; set; }

        /// <summary>The average size in bytes of blocks delivered by that peer.</summary>
        internal readonly AverageCalculator averageSizeBytes;

        /// <summary>The average delay in seconds between asking this peer for a block and it being downloaded.</summary>
        internal readonly AverageCalculator averageDelaySeconds;

        /// <summary>Time when the last block was delivered.</summary>
        private DateTime? lastDeliveryTime;

        private readonly ILoggerFactory loggerFactory;

        private readonly ILogger logger;

        private readonly IBlockPuller blockPuller;

        private readonly IInitialBlockDownloadState ibdState;

        private readonly IDateTimeProvider dateTimeProvider;

        public BlockPullerBehavior(IBlockPuller blockPuller, IInitialBlockDownloadState ibdState, IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.ibdState = ibdState;
            this.dateTimeProvider = dateTimeProvider;
            this.QualityScore = SamplelessQualityScore;

            int samplesCount = ibdState.IsInitialBlockDownload() ? IbdSamplesCount : NormalSamplesCount;
            this.averageSizeBytes = new AverageCalculator(samplesCount);
            this.averageDelaySeconds = new AverageCalculator(samplesCount);
            this.SpeedBytesPerSecond = 0;
            this.lastDeliveryTime = null;

            this.blockPuller = blockPuller;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc/>
        public void AddSample(long blockSizeBytes, double delaySinceRequestedSeconds)
        {
            double adjustedDelay = delaySinceRequestedSeconds;

            if (this.lastDeliveryTime != null)
            {
                double deliveryDiff = (this.dateTimeProvider.GetUtcNow() - this.lastDeliveryTime).Value.TotalSeconds;

                adjustedDelay = Math.Min(delaySinceRequestedSeconds, deliveryDiff);
            }

            this.averageSizeBytes.AddSample(blockSizeBytes);
            this.averageDelaySeconds.AddSample(adjustedDelay);

            long speedPerSeconds = 0;

            if (this.averageDelaySeconds.Average > 0)
                speedPerSeconds = (long)(this.averageSizeBytes.Average / this.averageDelaySeconds.Average);

            if (speedPerSeconds > MaxSpeedBytesPerSecond)
                speedPerSeconds = MaxSpeedBytesPerSecond;

            this.SpeedBytesPerSecond = speedPerSeconds;
        }

        /// <inheritdoc/>
        public void Penalize(double delaySeconds, int notDeliveredBlocksCount)
        {
            int maxSamplesToPenalize = (int)(this.averageDelaySeconds.GetMaxSamples() * MaxSamplesPercentageToPenalize);
            int penalizeTimes = notDeliveredBlocksCount < maxSamplesToPenalize ? notDeliveredBlocksCount : maxSamplesToPenalize;
            if (penalizeTimes < 1)
                penalizeTimes = 1;

            this.logger.LogDebug("Peer will be penalized {0} times.", penalizeTimes);

            for (int i = 0; i < penalizeTimes; i++)
                this.AddSample(0, delaySeconds);
        }

        /// <inheritdoc/>
        public void OnIbdStateChanged(bool isIbd)
        {
            // Recalculates the max samples count that can be used for quality score calculation.
            int samplesCount = isIbd ? IbdSamplesCount : NormalSamplesCount;
            this.averageSizeBytes.SetMaxSamples(samplesCount);
            this.averageDelaySeconds.SetMaxSamples(samplesCount);
        }

        /// <inheritdoc/>
        public void RecalculateQualityScore(long bestSpeedBytesPerSecond)
        {
            if (bestSpeedBytesPerSecond == 0)
                this.QualityScore = MaxQualityScore;
            else
                this.QualityScore = (double)this.SpeedBytesPerSecond / bestSpeedBytesPerSecond;

            if (this.QualityScore < MinQualityScore)
                this.QualityScore = MinQualityScore;

            if (this.QualityScore > MaxQualityScore)
                this.QualityScore = MaxQualityScore;

            this.logger.LogTrace("Quality score was set to {0}.", this.QualityScore);
        }

        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (message.Message.Payload is BlockPayload block)
            {
                block.Obj.Header.PrecomputeHash(true, true);
                uint256 blockHash = block.Obj.GetHash();

                this.logger.LogTrace("Block '{0}' delivered.", blockHash);

                this.blockPuller.PushBlock(blockHash, block.Obj, peer.Connection.Id);
                this.lastDeliveryTime = this.dateTimeProvider.GetUtcNow();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task RequestBlocksAsync(List<uint256> hashes)
        {
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
        }

        /// <inheritdoc />
        [NoTrace]
        public override object Clone()
        {
            return new BlockPullerBehavior(this.blockPuller, this.ibdState, this.dateTimeProvider, this.loggerFactory);
        }

        /// <inheritdoc />
        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        /// <inheritdoc />
        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }
    }
}
