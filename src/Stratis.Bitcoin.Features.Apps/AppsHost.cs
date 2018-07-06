using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;

namespace Stratis.Bitcoin.Features.Apps
{
    public class AppsHost : IAppsHost
    {
        private int seedPort = 32500;
        private readonly ILogger logger;
        private readonly List<(StratisApp, IWebHost)> hostedApps = new List<(StratisApp, IWebHost)>();

        public AppsHost(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Host(IEnumerable<StratisApp> stratisApps) =>
            stratisApps.Where(x => x.IsSinglePageApp).ToList().ForEach(this.HostSinglePageApp);        

        private void HostSinglePageApp(StratisApp stratisApp)
        {
            try
            {
                stratisApp.Address = $"http://localhost:{this.seedPort}";

                (StratisApp, IWebHost) pair = (stratisApp, new WebHostBuilder()
                            .UseKestrel()
                            .UseIISIntegration()
                            .UseWebRoot(Path.Combine(stratisApp.Location, stratisApp.WebRoot))
                            .UseContentRoot(Directory.GetCurrentDirectory())
                            .UseUrls(stratisApp.Address)
                            .UseStartup<SinglePageStartup>()
                            .Build());

                pair.Item2.Start();

                this.hostedApps.Add(pair);
                this.seedPort++;

                this.logger.LogError($"SPA '{stratisApp.DisplayName}' hosted at {stratisApp.Address}");
            }
            catch (Exception e)
            {
                this.logger.LogError($"Failed to host app '{stratisApp.DisplayName}' : {e.Message}");
            }
        }
    }
}
