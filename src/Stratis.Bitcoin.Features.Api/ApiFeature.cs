using System;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// Provides an Api to the full node
    /// </summary>
    public sealed class ApiFeature : FullNodeFeature
    {
        /// <summary>How long we are willing to wait for the API to stop.</summary>
        private const int ApiStopTimeoutSeconds = 10;

        private readonly IFullNodeBuilder fullNodeBuilder;

        private readonly FullNode fullNode;

        private readonly ApiSettings apiSettings;

        private readonly ApiFeatureOptions apiFeatureOptions;

        private readonly ILogger logger;

        private IWebHost webHost;

        public ApiFeature(
            IFullNodeBuilder fullNodeBuilder,
            FullNode fullNode,
            ApiFeatureOptions apiFeatureOptions,
            ApiSettings apiSettings,
            ILoggerFactory loggerFactory)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.apiFeatureOptions = apiFeatureOptions;
            apiSettings.Load(fullNode.Settings);
            this.apiSettings = apiSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("API starting on URL '{0}'.", this.apiSettings.ApiUri);
            this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode, this.apiSettings);

            // Start the keepalive timer, if set.
            // If the timer expires, the node will shut down.
            if (this.apiFeatureOptions.KeepaliveTimer != null)
            {
                this.apiFeatureOptions.KeepaliveTimer.Elapsed += (sender, args) =>
                {
                    this.logger.LogInformation($"The application will shut down because the keepalive timer has elapsed.");

                    this.apiFeatureOptions.KeepaliveTimer.Stop();
                    this.apiFeatureOptions.KeepaliveTimer.Enabled = false;
                    this.fullNode.Dispose();
                };

                this.apiFeatureOptions.KeepaliveTimer.Start();
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // Make sure we are releasing the listening ip address / port.
            if (this.webHost != null)
            {
                this.logger.LogInformation("API stopping on URL '{0}'.", this.apiSettings.ApiUri);
                this.webHost.StopAsync(TimeSpan.FromSeconds(ApiStopTimeoutSeconds)).Wait();
                this.webHost = null;
            }
        }
    }

    public sealed class ApiFeatureOptions
    {
        public Timer KeepaliveTimer { get; private set; }

        public void Keepalive(TimeSpan timeSpan)
        {
            this.KeepaliveTimer = new Timer
            {
                AutoReset = false,
                Interval = timeSpan.TotalSeconds * 1000
            };
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class ApiFeatureExtension
    {
        public static IFullNodeBuilder UseApi(this IFullNodeBuilder fullNodeBuilder, Action<ApiSettings> setup = null, Action<ApiFeatureOptions> optionsAction = null)
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
                        services.AddSingleton<ApiSettings>(new ApiSettings(setup));
                    });
            });

            return fullNodeBuilder;
        }
    }
}
