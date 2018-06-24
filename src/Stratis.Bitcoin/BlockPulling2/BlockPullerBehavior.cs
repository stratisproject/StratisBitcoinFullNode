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

namespace Stratis.Bitcoin.BlockPulling2
{
    /// <summary>
    /// Relation of the node's puller to a network peer node.
    /// Keeps all peer-related values that <see cref="BlockPuller"/> needs to know about a peer.
    /// </summary>
    public class BlockPullerBehavior : NetworkPeerBehavior
    {
        private const double MinQualityScore = 0.01;
        private const double MaxQualityScore = 1.0;

        /// <summary>Default quality score used when there are no samples to calculate the quality score.</summary>
        private const double SamplelessQualityScore = 0.3;

        /// <summary>Maximum number of samples that can be used for quality score calculation when node is in IBD.</summary>
        private const int IbdSamplesCount = 200;

        /// <summary>Maximum number of samples that can be used for quality score calculation when node is not in IBD.</summary>
        private const int NormalSamplesCount = 10;
        
        /// <summary>The maximum percentage of samples that can be used when peer is being penalized for not delivering blocks.</summary>
        /// <remarks><c>1</c> is 100%, <c>0</c> is 0%.</remarks>
        private const double MaxSamplesPercentageToPenalize = 0.1; //TODO test it and find best value

        /// <summary>Relative quality score of a peer.</summary>
        /// <remarks>It's a value from <see cref="MinQualityScore"/> to <see cref="MaxQualityScore"/>.</remarks>
        public double QualityScore { get; private set; }

        /// <summary>Upload speed of a peer in bytes per second.</summary>
        public int SpeedBytesPerSecond { get; private set; }

        /// <summary>Tip claimed by peer.</summary>
        public ChainedHeader Tip { get; set; }

        /// <summary>The average size in bytes of blocks delivered by that peer.</summary>
        private readonly AverageCalculator averageSizeBytes;

        /// <summary>The average delay in seconds between asking this peer for a block and it being downloaded.</summary>
        private readonly AverageCalculator averageDelaySeconds;

        /// <inheritdoc cref="ILoggerFactory"/>
        private readonly ILoggerFactory loggerFactory;

        /// <inheritdoc cref="ILogger"/>
        private readonly ILogger logger;

        /// <inheritdoc cref="BlockPuller"/>
        private readonly BlockPuller blockPuller;

        /// <inheritdoc cref="IInitialBlockDownloadState"/>
        private readonly IInitialBlockDownloadState ibdState;

        public BlockPullerBehavior(BlockPuller blockPuller, IInitialBlockDownloadState ibdState, ILoggerFactory loggerFactory)
        {
            this.ibdState = ibdState;
            this.QualityScore = SamplelessQualityScore;

            int samplesCount = ibdState.IsInitialBlockDownload() ? IbdSamplesCount : NormalSamplesCount;
            this.averageSizeBytes = new AverageCalculator(samplesCount);
            this.averageDelaySeconds = new AverageCalculator(samplesCount);
            this.SpeedBytesPerSecond = 0;

            this.blockPuller = blockPuller;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Adds peer performance sample that is used to estimate peer's qualities.
        /// </summary>
        /// <param name="blockSizeBytes">Block size in bytes.</param>
        /// <param name="delaySeconds">Time in seconds it took peer to deliver a block.</param>
        public void AddSample(long blockSizeBytes, double delaySeconds)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(blockSizeBytes), blockSizeBytes, nameof(delaySeconds), delaySeconds);

            this.averageSizeBytes.AddSample(blockSizeBytes);
            this.averageDelaySeconds.AddSample(delaySeconds);
            
            this.SpeedBytesPerSecond = (int)(this.averageSizeBytes.Average / this.averageDelaySeconds.Average);
            
            this.logger.LogTrace("(-):{0}={1}", nameof(this.SpeedBytesPerSecond), this.SpeedBytesPerSecond);
        }

        /// <summary>Applies a penalty to a peer for not delivering a block.</summary>
        /// <param name="delaySeconds">Time in which peer didn't deliver assigned blocks.</param>
        /// <param name="notDeliveredBlocksCount">Number of blocks peer failed to deliver.</param>
        public void Penalize(double delaySeconds, int notDeliveredBlocksCount)
        {
            this.logger.LogTrace("({0}:{1},{2}:{3})", nameof(delaySeconds), delaySeconds, nameof(notDeliveredBlocksCount), notDeliveredBlocksCount);

            int maxSamplesToPenalize = (int)(this.averageDelaySeconds.GetMaxSamples() * MaxSamplesPercentageToPenalize);
            int penalizeTimes = notDeliveredBlocksCount < maxSamplesToPenalize ? notDeliveredBlocksCount : maxSamplesToPenalize;
            
            this.logger.LogDebug("Peer will be penalized {0} times.", penalizeTimes);

            for (int i = 0; i < penalizeTimes; i++)
                this.AddSample(0, delaySeconds);

            this.logger.LogTrace("(-)");
        }

        /// <summary>Called when IBD state changed.</summary>
        public void OnIbdStateChanged(bool isIbd)
        {
            this.logger.LogTrace("({0}:{1})", nameof(isIbd), isIbd);

            // Recalculates the max samples count that can be used for quality score calculation.
            int samplesCount = isIbd ? IbdSamplesCount : NormalSamplesCount;
            this.averageSizeBytes.SetMaxSamples(samplesCount);
            this.averageDelaySeconds.SetMaxSamples(samplesCount);

            this.logger.LogTrace("(-)");
        }
        
        /// <summary>Recalculates the quality score for this peer.</summary>
        /// <param name="bestSpeedBytesPerSecond">Speed in bytes per second that is considered to be the maximum speed.</param>
        public void RecalculateQualityScore(int bestSpeedBytesPerSecond)
        {
            this.logger.LogTrace("({0}:{1})", nameof(bestSpeedBytesPerSecond), bestSpeedBytesPerSecond);

            this.QualityScore = (double)this.SpeedBytesPerSecond / bestSpeedBytesPerSecond;

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

        /// <summary>Requests blocks from this peer.</summary>
        /// <param name="hashes">Hashes of blocks that should be asked to be delivered.</param>
        /// <exception cref="OperationCanceledException">Thrown in case peer is in the wrong state or TCP connection was closed during sending a message.</exception>
        public async Task RequestBlocksAsync(List<uint256> hashes)
        {
            this.logger.LogTrace("({0}.{1}:{2})", nameof(hashes), nameof(hashes.Count), hashes.Count);

            var getDataPayload = new GetDataPayload();

            foreach (uint256 uint256 in hashes)
            {
                var vector = new InventoryVector(InventoryType.MSG_BLOCK, uint256);
                vector.Type = this.AttachedPeer.AddSupportedOptions(vector.Type);

                getDataPayload.Inventory.Add(vector);
            }

            if (this.AttachedPeer.State != NetworkPeerState.HandShaked)
            {
                this.logger.LogTrace("(-)[ATTACHED_PEER]");
                throw new OperationCanceledException("Peer is in the wrong state!");
            }

            await this.AttachedPeer.SendMessageAsync(getDataPayload).ConfigureAwait(false);

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
