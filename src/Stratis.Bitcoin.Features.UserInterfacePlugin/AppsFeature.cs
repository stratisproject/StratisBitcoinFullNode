using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsFeature : FullNodeFeature
    {
        private readonly ILogger logger;
        private readonly IAppStore appStore;

        public AppsFeature(ILoggerFactory loggerFactory, IAppStore appStore)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.appStore = appStore;
        }

        public override void Initialize()
        {
            this.logger.LogInformation($"Initializing {nameof(AppsFeature)}");


        }
    }

    public static class FullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseApps(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<AppsFeature>("apps");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<AppsFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IAppStore, AppStore>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
