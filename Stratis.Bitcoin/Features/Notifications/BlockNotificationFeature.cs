using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Notifications.Controllers;

namespace Stratis.Bitcoin.Features.Notifications
{
    /// <summary>
    /// Feature enabling the broadcasting of blocks.
    /// </summary>
    public class BlockNotificationFeature : FullNodeFeature
    {
        private readonly BlockNotification blockNotification;
        private readonly IConnectionManager connectionManager;
        private readonly LookaheadBlockPuller blockPuller;
        private readonly ChainState chainState;
        private readonly ConcurrentChain chain;

        public BlockNotificationFeature(BlockNotification blockNotification, IConnectionManager connectionManager, 
            LookaheadBlockPuller blockPuller, ChainState chainState, ConcurrentChain chain)
        {
            this.blockNotification = blockNotification;
            this.connectionManager = connectionManager;
            this.blockPuller = blockPuller;
            this.chainState = chainState;
            this.chain = chain;
        }

        public override void Start()
        {
            var connectionParameters = this.connectionManager.Parameters;
            connectionParameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, new LoggerFactory()));
            this.blockNotification.Notify();
            this.chainState.HighestValidatedPoW = this.chain.Genesis;
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseBlockNotification(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<BlockNotificationFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton<BlockNotification>();
                    services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>());
                    services.AddSingleton<NotificationsController>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
