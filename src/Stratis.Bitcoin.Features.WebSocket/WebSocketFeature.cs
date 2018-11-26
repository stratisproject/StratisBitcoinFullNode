using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.WebSocket
{
    public class WebSocketFeature : FullNodeFeature
    {
        private readonly IWebSocketService service;

        private readonly WebSocketSettings settings;

        private readonly ILoggerFactory loggerFactory;

        private readonly IFullNodeBuilder fullNodeBuilder;

        private readonly FullNode fullNode;

        public WebSocketFeature(IFullNodeBuilder fullNodeBuilder,
            FullNode fullNode,
            IWebSocketService service,
            WebSocketSettings settings,
            ILoggerFactory loggerFactory)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.service = service;
            this.settings = settings;
            this.loggerFactory = loggerFactory;

            this.loggerFactory.AddProvider(new WebSocketLoggingProvider(service, new WebSocketLoggerConfiguration()));
        }

        public override Task InitializeAsync()
        {
            //NetworkPeerConnectionParameters connectionParameters = this.connectionManager.Parameters;
            //connectionParameters.TemplateBehaviors.Add(new BlockPullerBehavior(this.blockPuller, this.loggerFactory));

            //this.blockNotification.Start();
            //this.chainState.ConsensusTip = this.chain.Tip;

            return this.service.StartAsync(this.fullNode, this.fullNodeBuilder.Services);

            //return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            this.service.Dispose();
            //this.blockNotification.Stop();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderWebSocketExtension
    {
        public static IFullNodeBuilder UseWebSocket(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<WebSocketFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton(fullNodeBuilder);
                    services.AddSingleton<WebSocketSettings>();
                    services.AddSingleton<IWebSocketService, WebSocketService>();

                    //services.AddSingleton<IBlockNotification, BlockNotification>();
                    //services.AddSingleton<LookaheadBlockPuller>().AddSingleton<ILookaheadBlockPuller, LookaheadBlockPuller>(provider => provider.GetService<LookaheadBlockPuller>());
                    //services.AddSingleton<NotificationsController>();
                });
            });

            return fullNodeBuilder;
        }
    }
}
