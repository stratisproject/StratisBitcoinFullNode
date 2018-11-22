using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.Bitcoin.Features.SignalR
{
    /// <summary>
    /// Use this feature if you need a publish only SignalR hub for realtime messaging.
    /// Messages can have a topic to filter the Json content of the messages on the client side.
    /// </summary>
    public sealed class SignalRFeature : FullNodeFeature
    {
        private readonly SignalRSettings signalRSettings;

        public ISignalRService SignalRService { get; }

        private readonly ILogger logger;

        public SignalRFeature(ISignalRService signalRService, SignalRSettings signalRSettings, ILoggerFactory loggerFactory)
        {
            this.signalRSettings = signalRSettings;
            this.SignalRService = signalRService;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc />
        public override Task InitializeAsync()
        {
            this.logger.LogInformation("SignalR hub starting at URL '{0}'.", this.SignalRService.HubRoute);
            return this.SignalRService.StartAsync();
        }

        public override void Dispose()
        {
            this.SignalRService.Dispose();
        }
    }

    public static class SignalRFeatureExtension
    {
        public static IFullNodeBuilder UseSignalR(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SignalRFeature>()
                    .FeatureServices(services =>
                        {
                            services.AddSingleton(fullNodeBuilder);
                            services.AddSingleton<SignalRSettings>();
                            services.AddSingleton<ISignalRService, SignalRService>();
                        });
            });

            return fullNodeBuilder;
        }
    }
}
