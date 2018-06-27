using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }
    }

    public class AppsFeature : FullNodeFeature
    {
        private readonly ILogger logger;
        private readonly IAppStore appStore;

        public AppsFeature(ILoggerFactory loggerFactory, IAppStore appStore)
        {
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.appStore = appStore;
        }

        public override void Initialize()
        {
            this.logger.LogInformation($"Initializing {nameof(AppsFeature)}");

            this.appStore.GetApplications().Subscribe(OnGetApplications);
        }

        private void OnGetApplications(IReadOnlyCollection<IStratisApp> stratisApps)
        {
            new WebHostBuilder()
                .UseKestrel()
                .UseIISIntegration()
                .UseWebRoot(@"C:\Development\app1\dist\app1")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseUrls("http://localhost:32500")
                .UseStartup<Startup>()
                .Build()
                .Start();
                
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
                        services.AddSingleton<IAppsFileService, AppsFileService>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
