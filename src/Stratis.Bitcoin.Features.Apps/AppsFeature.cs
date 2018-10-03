using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    /// <summary>
    /// Feature allowing for the dynamic hosting of web-based applications targeting the FullNode.
    /// </summary>
    public class AppsFeature : FullNodeFeature
    {
        private bool disposed;
        private readonly ILogger logger;
        private readonly IAppsStore appsStore;
        private readonly IAppsHost appsHost;
        private readonly DataFolder dataFolder;

        public AppsFeature(ILoggerFactory loggerFactory, IAppsStore appsStore, IAppsHost appsHost, DataFolder dataFolder)
        {
            this.appsStore = appsStore;
            this.appsHost = appsHost;
            this.dataFolder = dataFolder;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override Task InitializeAsync()
        {
            this.logger.LogInformation("Initializing {0}.", nameof(AppsFeature));

            Directory.CreateDirectory(this.dataFolder.ApplicationsPath);

            this.appsHost.Host(this.appsStore.Applications);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            if (this.disposed)
                return;
            this.logger.LogInformation("Disposing {0}.", nameof(AppsFeature));

            this.appsHost.Close();

            base.Dispose();
            this.disposed = true;
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
                        services.AddSingleton<IAppsHost, AppsHost>();
                        services.AddSingleton<AppsController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
