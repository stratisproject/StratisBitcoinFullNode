using System;
using System.Linq;
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

namespace Stratis.Bitcoin.Features.SignalR
{
    public class SignalRFeature : FullNodeFeature
    {
        internal static Dictionary<Type, ClientEventBroadcasterSettings> eventBroadcasterSettings;
        private const int SignalRStopTimeoutSeconds = 10;
        private readonly FullNode fullNode;
        private readonly IFullNodeBuilder fullNodeBuilder;
        private readonly SignalRSettings settings;
        private readonly IEnumerable<IClientEventBroadcaster> eventBroadcasters;
        private readonly IEventsSubscriptionService eventsSubscriptionService;
        private IWebHost webHost;
        private readonly ILogger<SignalRFeature> logger;

        public SignalRFeature(
            FullNode fullNode,
            IFullNodeBuilder fullNodeBuilder,
            SignalRSettings settings,
            ILoggerFactory loggerFactory,
            IEnumerable<IClientEventBroadcaster> eventBroadcasters,
            IEventsSubscriptionService eventsSubscriptionService)
        {
            this.fullNode = fullNode;
            this.fullNodeBuilder = fullNodeBuilder;
            this.settings = settings;
            this.eventBroadcasters = eventBroadcasters;
            this.eventsSubscriptionService = eventsSubscriptionService;
            this.logger = loggerFactory.CreateLogger<SignalRFeature>();
        }

        public override Task InitializeAsync()
        {
            this.webHost = Program.Initialize(this.fullNodeBuilder.Services, this.fullNode, this.settings,
                new WebHostBuilder());

            this.eventsSubscriptionService.Init();
            foreach (IClientEventBroadcaster clientEventBroadcaster in this.eventBroadcasters)
            {
                // Intialise with specified settings
                clientEventBroadcaster.Init(eventBroadcasterSettings[clientEventBroadcaster.GetType()]);
            }

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
        public IClientEvent[] EventsToHandle { get; set; }

        public (Type Broadcaster, ClientEventBroadcasterSettings clientEventBroadcasterSettings)[]
            ClientEventBroadcasters { get; set; }
    }

    public class ClientEventBroadcasterSettings
    {
        public int BroadcastFrequencySeconds { get; set; }
    }

    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder AddSignalR(this IFullNodeBuilder fullNodeBuilder,
            Action<SignalROptions> optionsAction = null)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SignalRFeature>("signalr");
            var options = new SignalROptions();
            optionsAction?.Invoke(options);
            SignalRFeature.eventBroadcasterSettings =
                options.ClientEventBroadcasters.ToDictionary(
                    pair => pair.Broadcaster, pair => pair.clientEventBroadcasterSettings);

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SignalRFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IEventsSubscriptionService, EventSubscriptionService>();
                        services.AddSingleton<EventsHub>();
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton(options);
                        services.AddSingleton<SignalRSettings>();

                        if (null != options.ClientEventBroadcasters)
                        {
                            foreach (var eventBroadcaster in options.ClientEventBroadcasters)
                            {
                                if (typeof(IClientEventBroadcaster).IsAssignableFrom(eventBroadcaster.Broadcaster))
                                {
                                    services.AddSingleton(typeof(IClientEventBroadcaster),
                                        eventBroadcaster.Broadcaster);
                                }
                                else
                                {
                                    Console.WriteLine(
                                        $"Warning {eventBroadcaster.Broadcaster.Name} is not of type {typeof(IClientEventBroadcaster).Name}");
                                }
                            }
                        }
                    });
            });

            return fullNodeBuilder;
        }
    }
}