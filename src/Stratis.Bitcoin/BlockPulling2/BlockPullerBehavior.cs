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
    public class BlockPullerBehavior : NetworkPeerBehavior
    {
        private const double MinQualityScore = 0.01;
        private const double SamplelessQualityScore = 0.3;
        private const double MaxQualityScore = 1.0;

        private const int IBDSamplesCount = 200;
        private const int NormalSamplesCount = 10;

        public double QualityScore { get; private set; }
        public int SpeedBytesPerSecond { get; private set; }
        
        private readonly AverageCalculator averageSizeBytes;
        private readonly AverageCalculator averageDelaySeconds;
        
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly BlockPuller blockPuller;

        private readonly IInitialBlockDownloadState ibdState;

        public BlockPullerBehavior(BlockPuller blockPuller, IInitialBlockDownloadState ibdState, ILoggerFactory loggerFactory)
        {
            this.QualityScore = SamplelessQualityScore;

            this.averageSizeBytes = new AverageCalculator(IBDSamplesCount);
            this.averageDelaySeconds = new AverageCalculator(IBDSamplesCount);
            this.SpeedBytesPerSecond = 0;
            this.ibdState = ibdState;

            this.blockPuller = blockPuller;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;
        }

        public void AddSample(long blockSizeBytes, double delaySeconds)
        {
            this.averageSizeBytes.AddSample(blockSizeBytes);
            this.averageDelaySeconds.AddSample(delaySeconds);

            this.AdjustMaxSamples();

            this.SpeedBytesPerSecond = (int)(this.averageSizeBytes.Average / this.averageDelaySeconds.Average);
        }

        public void Penalize(double delay, int notDeliveredBlocksCount)
        {
            int tenPercentOfSamples = (int)(this.averageDelaySeconds.GetMaxSamples() * 0.1); //TODO Move to constant, test it and find best value
            int penalizeTimes = (notDeliveredBlocksCount < tenPercentOfSamples) ? notDeliveredBlocksCount : tenPercentOfSamples;

            for (int i = 0; i < penalizeTimes; ++i)
            {
                this.averageSizeBytes.AddSample(0);
                this.averageDelaySeconds.AddSample(delay);
            }

            this.AdjustMaxSamples();

            this.SpeedBytesPerSecond = (int)(this.averageSizeBytes.Average / this.averageDelaySeconds.Average);
        }

        private void AdjustMaxSamples()
        {
            int samplesCount = this.ibdState.IsInitialBlockDownload() ? IBDSamplesCount : NormalSamplesCount;
            this.averageSizeBytes.SetMaxSamples(samplesCount);
            this.averageDelaySeconds.SetMaxSamples(samplesCount);
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
            return new BlockPullerBehavior(this.blockPuller, this.ibdState, this.loggerFactory);
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
    }
}
