using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

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

        private readonly ICertificateStore certificateStore;

        public ApiFeature(
            IFullNodeBuilder fullNodeBuilder,
            FullNode fullNode,
            ApiFeatureOptions apiFeatureOptions,
            ApiSettings apiSettings,
            ILoggerFactory loggerFactory,
            ICertificateStore certificateStore)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.apiFeatureOptions = apiFeatureOptions;
            this.apiSettings = apiSettings;
            this.certificateStore = certificateStore;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.InitializeBeforeBase = true;
        }

        public override Task InitializeAsync()
        {
            this.logger.LogInformation("API starting on URL '{0}'.", this.apiSettings.ApiUri);
            this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode, this.apiSettings, this.certificateStore, new WebHostBuilder());

            if (this.apiSettings.KeepaliveTimer == null)
            {
                this.logger.LogTrace("(-)[KEEPALIVE_DISABLED]");
                return Task.CompletedTask;
            }

            // Start the keepalive timer, if set.
            // If the timer expires, the node will shut down.
            this.apiSettings.KeepaliveTimer.Elapsed += (sender, args) =>
            {
                this.logger.LogInformation($"The application will shut down because the keepalive timer has elapsed.");

                this.apiSettings.KeepaliveTimer.Stop();
                this.apiSettings.KeepaliveTimer.Enabled = false;
                this.fullNode.NodeLifetime.StopApplication();
            };

            this.apiSettings.KeepaliveTimer.Start();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            ApiSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            ApiSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // Make sure the timer is stopped and disposed.
            if (this.apiSettings.KeepaliveTimer != null)
            {
                this.apiSettings.KeepaliveTimer.Stop();
                this.apiSettings.KeepaliveTimer.Enabled = false;
                this.apiSettings.KeepaliveTimer.Dispose();
            }

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
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
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
                        services.AddSingleton<ApiSettings>();
                        services.AddSingleton<ICertificateStore, CertificateStore>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
