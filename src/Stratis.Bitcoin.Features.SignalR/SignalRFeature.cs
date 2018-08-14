using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;

namespace Stratis.Bitcoin.Features.SignalR
{
    /// <summary>
    /// Feature allowing for the dynamic hosting of web-based applications targeting the FullNode.
    /// </summary>
    public class SignalRFeature : FullNodeFeature
    {
        private readonly ILogger logger;

        public SignalRFeature(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override void Initialize()
        {
            this.logger.LogInformation("Initializing {0}", nameof(SignalRFeature));
        }
    }

    public static class FullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseSignalR(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SignalRFeature>("signalr");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SignalRFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<ISignalRService, SignalRService>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
