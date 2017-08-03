using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Api.Models;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Api
{
    /// <summary>
    /// Provides an Api to the full node
    /// </summary>
    public class ApiFeature : FullNodeFeature
    {		
        private readonly IFullNodeBuilder fullNodeBuilder;
        private readonly FullNode fullNode;
        private readonly ApiFeatureOptions apiFeatureOptions;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly ILogger logger;

        public ApiFeature(
            IFullNodeBuilder fullNodeBuilder, 
            FullNode fullNode, 
            ApiFeatureOptions apiFeatureOptions, 
            IAsyncLoopFactory asyncLoopFactory,
            ILoggerFactory loggerFactory)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.apiFeatureOptions = apiFeatureOptions;
            this.asyncLoopFactory = asyncLoopFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Start()
        {
            this.logger.LogInformation($"Api starting on url {this.fullNode.Settings.ApiUri}");
            Program.Initialize(this.fullNodeBuilder.Services, this.fullNode);

            this.TryStartKeepaliveMonitor();
        }

        /// <summary>
        /// A KeepaliveMonitor when enabled will shutdown the
        /// node if no one is calling the keepalive endpoint 
        /// during a certain trashold window
        /// </summary>
        public void TryStartKeepaliveMonitor()
        {
            if (this.apiFeatureOptions.KeepaliveMonitor?.KeepaliveInterval.TotalSeconds > 0)
            {
                this.asyncLoopFactory.Run("ApiFeature.KeepaliveMonitor", token =>
                    {
                        // shortened for redability
                        var monitor = this.apiFeatureOptions.KeepaliveMonitor;

                        // check the trashold to trigger a shutdown
                        if (monitor.LastBeat.Add(monitor.KeepaliveInterval) < DateTime.UtcNow)
                            this.fullNode.Stop();

                        return Task.CompletedTask;
                    },
                    this.fullNode.NodeLifetime.ApplicationStopping,
                    repeatEvery: this.apiFeatureOptions.KeepaliveMonitor?.KeepaliveInterval,
                    startAfter: TimeSpans.Minute);
            }
        }
    }

    public class ApiFeatureOptions
    {
        public KeepaliveMonitor KeepaliveMonitor { get; set; }

        public void Keepalive(TimeSpan timeSpan)
        {
            this.KeepaliveMonitor = new KeepaliveMonitor {KeepaliveInterval = timeSpan};
        }
    }

    public static class ApiFeatureExtension
    {
        public static IFullNodeBuilder UseApi(this IFullNodeBuilder fullNodeBuilder, Action<ApiFeatureOptions> optionsAction = null)
        {
            // TODO: move the options in to the feature builder
            var options = new ApiFeatureOptions();
            optionsAction?.Invoke(options);

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<ApiFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton(options);
                    });
            });

            return fullNodeBuilder;
        }
    }	
}
