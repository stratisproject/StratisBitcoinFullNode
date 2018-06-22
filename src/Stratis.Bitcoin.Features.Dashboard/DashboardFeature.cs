using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Api;

namespace Stratis.Bitcoin.Features.Dashboard
{
    public class DashboardFeature : FullNodeFeature
    {

        private readonly ILogger logger;
        private readonly DashboardApiSettings apiSettings;
        private readonly FullNode fullNode;
        private IWebHost host;
        private const int ApiStopTimeoutSeconds = 10;

        public static void Main(string[] args){}

        public DashboardFeature(ILoggerFactory loggerFactory, DashboardApiSettings apiSettings, FullNode fullNode) 
        {
            this.apiSettings = apiSettings;
            this.fullNode = fullNode;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName); 
        }

        public override void Initialize()
        {
            this.apiSettings.Load(this.fullNode.Settings);

            Uri apiUri = this.apiSettings.ApiUri;

            this.host = new WebHostBuilder()
                .UseKestrel()
                .UseIISIntegration()
                .UseUrls(apiUri.ToString())
                .UseStartup<Startup>()
                .Build();

            this.host.Start();

            this.logger.LogInformation("Stratis Dashboard Initialized on http://localhost:" + this.apiSettings.ApiPort);
            
        }

        public override void Dispose()
        {
            if (this.apiSettings.KeepaliveTimer != null)
            {
                this.apiSettings.KeepaliveTimer.Stop();
                this.apiSettings.KeepaliveTimer.Enabled = false;
                this.apiSettings.KeepaliveTimer.Dispose();
            }

            if (this.host != null)
            {
                this.logger.LogInformation("Dashboard stopping on URL '{0}'.", this.apiSettings.ApiUri);
                this.host.StopAsync(TimeSpan.FromSeconds(ApiStopTimeoutSeconds)).Wait();
                this.host = null;
            }
        }

    }

    public static class DashboardFeatureExtension
    {
        public static IFullNodeBuilder UseDashboard(this IFullNodeBuilder fullNodeBuilder, Action<DashboardApiSettings> setup = null)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<DashboardFeature>()
                .DependOn<ApiFeature>()
                .FeatureServices(services =>
                 {
                     services.AddSingleton(fullNodeBuilder);
                     services.AddSingleton(new DashboardApiSettings(setup));
                 });
            });
            return fullNodeBuilder;
        }
    }
}
