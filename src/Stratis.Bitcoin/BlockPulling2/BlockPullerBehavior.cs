using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.BlockPulling2
{
    public class BlockPullerBehavior : NetworkPeerBehavior
    {
        private const double MinQualityScore = 0.01;
        private const double SamplelessQualityScore = 0.3;
        private const double MaxQualityScore = 1.0;

        private const int MaxSamples = 100;

        public double QualityScore { get; private set; }
        public int SpeedBytesPerSecond { get; private set; }

        private readonly CircularArray<SizeDelaySample> blockSizeDelaySecondsSamples;

        private double averageSizeBytes;
        private double averageDelaySeconds;

        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly BlockPuller blockPuller;

        public BlockPullerBehavior(BlockPuller blockPuller, ILoggerFactory loggerFactory)
        {
            this.blockSizeDelaySecondsSamples = new CircularArray<SizeDelaySample>(MaxSamples);
            this.QualityScore = SamplelessQualityScore;

            this.averageSizeBytes = 0;
            this.averageDelaySeconds = 0;
            this.SpeedBytesPerSecond = 0;

            this.blockPuller = blockPuller;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        public void AddSample(long blockSizeBytes, double delaySeconds)
        {
            var newSample = new SizeDelaySample()
            {
                SizeBytes = blockSizeBytes,
                DelaySeconds = delaySeconds
            };

            this.blockSizeDelaySecondsSamples.Add(newSample, out SizeDelaySample unused);

            this.averageSizeBytes = CircularArray<double>.RecalculateAverageForSircularArray(this.blockSizeDelaySecondsSamples.Count, this.averageSizeBytes, blockSizeBytes, unused.SizeBytes);
            this.averageDelaySeconds = CircularArray<double>.RecalculateAverageForSircularArray(this.blockSizeDelaySecondsSamples.Count, this.averageDelaySeconds, delaySeconds, unused.DelaySeconds);

            this.SpeedBytesPerSecond = (int)(this.averageSizeBytes / this.averageDelaySeconds);
        }

        public void RecalculateQualityScore(int bestSpeedBytesPerSecond)
        {
            this.QualityScore = (double)this.SpeedBytesPerSecond / bestSpeedBytesPerSecond;

            if (this.QualityScore < MinQualityScore)
                this.QualityScore = MinQualityScore;

            if (this.QualityScore > MaxQualityScore)
                this.QualityScore = MaxQualityScore;
        }
        
        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(message), message.Message.Command);

            if (message.Message.Payload is BlockPayload block)
            {
                this.blockPuller.PushBlock(block.Obj.GetHash(), block.Obj, this.AttachedPeer.Connection.Id);
            }

            this.logger.LogTrace("(-)");
        }

        public async Task RequestBlocksAsync(List<uint256> hashes)
        {
            var getDataPayload = new GetDataPayload();

            foreach (uint256 uint256 in hashes)
            {
                var vector = new InventoryVector(InventoryType.MSG_BLOCK, uint256);
                vector.Type = this.AttachedPeer.AddSupportedOptions(vector.Type);

                getDataPayload.Inventory.Add(vector);
            }

            if ((this.AttachedPeer == null) || (this.AttachedPeer.State != NetworkPeerState.HandShaked))
            {
                this.logger.LogTrace("(-)[ATTACHED_PEER]");
                throw new Exception("Peer is in the wrong state!");
            }

            await this.AttachedPeer.SendMessageAsync(getDataPayload).ConfigureAwait(false);

            this.logger.LogTrace("(-)");
        }

        /// <inheritdoc />
        public override object Clone()
        {
            return new BlockPullerBehavior(this.blockPuller, this.loggerFactory);
        }

        protected override void AttachCore()
        {
            this.logger.LogTrace("()");

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);

            this.logger.LogTrace("(-)");
        }
        
        protected override void DetachCore()
        {
            this.logger.LogTrace("()");
            
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            
            this.logger.LogTrace("(-)");
        }

        private struct SizeDelaySample
        {
            public long SizeBytes;
            public double DelaySeconds;
        }
    }
}
