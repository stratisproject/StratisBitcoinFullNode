using System;
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
        private readonly IAppsStore appsStore;
        private readonly IAppsHost appsHost;

        public AppsFeature(ILoggerFactory loggerFactory, IAppsStore appsStore, IAppsHost appsHost)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.appsStore = appsStore;
            this.appsHost = appsHost;
        }

        public override void Initialize()
        {
            this.logger.LogInformation($"Initializing {nameof(AppsFeature)}");

            this.appsStore.GetApplications().Subscribe(x => this.appsHost.Host(x));
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
                        services.AddSingleton<IAppsStore, AppsStore>();
                        services.AddSingleton<IAppsFileService, AppsFileService>();
                        services.AddSingleton<IAppsHost, AppsHost>();
                        services.AddSingleton<AppsController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
