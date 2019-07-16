using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.PoA.Events;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRFeature : FullNodeFeature
    {
        private readonly FullNode fullNode;
        private readonly IFullNodeBuilder fullNodeBuilder;
        private readonly SignalRSettings settings;
        private readonly EventsHub eventsHub;
        private IWebHost webHost;
        private readonly ILogger<SignalRFeature> logger;
        private const int SignalRStopTimeoutSeconds = 10;
        private readonly List<SubscriptionToken> subscriptions = new List<SubscriptionToken>();

        public SignalRFeature(
            FullNode fullNode,
            IFullNodeBuilder fullNodeBuilder,
            SignalRSettings settings,
            ILoggerFactory loggerFactory,
            ISignals signals,
            EventsHub eventsHub)
        {
            this.fullNode = fullNode;
            this.fullNodeBuilder = fullNodeBuilder;
            this.settings = settings;
            this.eventsHub = eventsHub;
            this.logger = loggerFactory.CreateLogger<SignalRFeature>();

            this.subscriptions.Add(signals.Subscribe<BlockConnected>(this.OnEvent));
            this.subscriptions.Add(signals.Subscribe<FedMemberAdded>(this.OnEvent));
            this.subscriptions.Add(signals.Subscribe<FedMemberKicked>(this.OnEvent));
            this.subscriptions.Add(signals.Subscribe<TransactionReceived>(this.OnEvent));
        }

        public override Task InitializeAsync()
        {
            this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode, this.settings, new WebHostBuilder());

            return Task.CompletedTask;
        }

        private void OnEvent(EventBase @event)
        {
            this.eventsHub.SendToClients(@event).ConfigureAwait(false).GetAwaiter().GetResult();
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
            this.subscriptions.ForEach(s => s?.Dispose());
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
                        services.AddSingleton<EventsHub>();
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton(options);
                        services.AddSingleton<SignalRSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}