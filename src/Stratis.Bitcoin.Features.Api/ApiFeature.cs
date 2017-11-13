using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Api.Models;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Provides an Api to the full node
    /// </summary>
    public sealed class ApiFeature : FullNodeFeature
    {
        /// <summary>The async loop we need to wait upon before we can shut down this feature.</summary>
        private IAsyncLoop asyncLoop;

        /// <summary>Factory for creating background async loop tasks.</summary>
        private readonly IAsyncLoopFactory asyncLoopFactory;

        /// <summary>How long we are willing to wait for the API to stop.</summary>
        private const int APIStopTimeoutSeconds = 10;

        private readonly IFullNodeBuilder fullNodeBuilder;
        private readonly FullNode fullNode;
        private readonly ApiFeatureOptions apiFeatureOptions;
        private readonly ILogger logger;
        private IWebHost webHost = null;

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
            this.logger.LogInformation("API starting on URL '{0}'.", this.fullNode.Settings.ApiUri);
            this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode);

            this.TryStartKeepaliveMonitor();
        }

        public override void Stop()
        {
            this.asyncLoop?.Dispose();

            // Make sure we are releasing the listening ip address / port.
            if (this.webHost != null)
            {
                this.logger.LogInformation("API stopping on URL '{0}'.", this.fullNode.Settings.ApiUri);
                this.webHost.StopAsync(TimeSpan.FromSeconds(APIStopTimeoutSeconds)).Wait();
                this.webHost = null;
            }        
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
                this.asyncLoop = this.asyncLoopFactory.Run("ApiFeature.KeepaliveMonitor", token =>
                {
                    // shortened for redability
                    KeepaliveMonitor monitor = this.apiFeatureOptions.KeepaliveMonitor;

                    // check the trashold to trigger a shutdown
                    if (monitor.LastBeat.Add(monitor.KeepaliveInterval) < this.fullNode.DateTimeProvider.GetUtcNow())
                        this.fullNode.Stop();

                    return Task.CompletedTask;
                },
                this.fullNode.NodeLifetime.ApplicationStopping,
                repeatEvery: this.apiFeatureOptions.KeepaliveMonitor?.KeepaliveInterval,
                startAfter: TimeSpans.Minute);
            }
        }
    }

    public sealed class ApiFeatureOptions
    {
        public KeepaliveMonitor KeepaliveMonitor { get; private set; }

        public void Keepalive(TimeSpan timeSpan)
        {
            this.KeepaliveMonitor = new KeepaliveMonitor { KeepaliveInterval = timeSpan };
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