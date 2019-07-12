using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRFeature : FullNodeFeature
    {
        private readonly FullNode fullNode;
        private readonly IFullNodeBuilder fullNodeBuilder;
        private readonly SignalRSettings settings;
        private IWebHost webHost;
        private readonly ILogger<SignalRFeature> logger;
        private const int SignalRStopTimeoutSeconds = 10;

        public SignalRFeature(FullNode fullNode, IFullNodeBuilder fullNodeBuilder, SignalRSettings settings, ILoggerFactory loggerFactory)
        {
            this.fullNode = fullNode;
            this.fullNodeBuilder = fullNodeBuilder;
            this.settings = settings;
            this.logger = loggerFactory.CreateLogger<SignalRFeature>();
        }
        
        public override Task InitializeAsync()
        {
            this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode, this.settings, new WebHostBuilder());
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
           SignalRSettings.PrintHelp(network);
        }
        
        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            SignalRSettings.BuildDefaultConfigurationFile(builder, network);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            // Make sure we are releasing the listening ip address / port.
            if (this.webHost == null) return;
            this.logger.LogInformation("API stopping on URL '{0}'.", this.settings.SignalRUri);
            this.webHost.StopAsync(TimeSpan.FromSeconds(SignalRStopTimeoutSeconds)).Wait();
            this.webHost = null;
        }
    }
    
    public class SignalROptions
    {
    }

    public static partial class IFullNodeBuilderExtensions
    {
         public static IFullNodeBuilder AddSignalR(this IFullNodeBuilder fullNodeBuilder, Action<SignalROptions> optionsAction = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SignalRFeature>("signalr");
            var options = new SignalROptions();
            optionsAction?.Invoke(options);

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SignalRFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton(options);
                        services.AddSingleton<SignalRSettings>();
                    });
            });
            
            return fullNodeBuilder;
        }
    }
}