using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Notifications
{
    /// <summary>
    /// Feature enabling the broadcasting of blocks.
    /// </summary>
    public class BlockNotificationFeature : FullNodeFeature
    {
        private readonly BlockNotification blockNotification;

        private readonly uint256 startHash;

        private readonly FullNode.CancellationProvider cancellationProvider;

        public BlockNotificationFeature(BlockNotification blockNotification, BlockNotificationStartHash blockNotificationStartHash, 
			FullNode.CancellationProvider cancellationProvider)
        {
            this.blockNotification = blockNotification;
            this.startHash = blockNotificationStartHash.StartHash;
            this.cancellationProvider = cancellationProvider;
        }
        
        public override void Start()
        {           
            this.blockNotification.Notify(this.startHash, this.cancellationProvider.Cancellation.Token);
        }
    }

    public static class BlockNotificationFeatureExtension
    {
        public static IFullNodeBuilder UseBlockNotification(this IFullNodeBuilder fullNodeBuilder, uint256 startHash)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<BlockNotificationFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton(new BlockNotificationStartHash(startHash));
                        services.AddSingleton<BlockNotification>();
                        services.AddSingleton<Signals>();
                        services.AddSingleton<BlockPuller, LookaheadBlockPuller>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
