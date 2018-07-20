using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Features.Apps.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Apps
{
    /// <summary>
    /// Reponsible for web-hosting StratisApps
    /// </summary>
    public class AppsHost : IDisposable, IAppsHost
    {        
        private readonly ILogger logger;
        private readonly List<(IStratisApp app, IWebHost host)> hostedApps;

        public AppsHost(ILoggerFactory loggerFactory) 
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.hostedApps = new List<(IStratisApp app, IWebHost host)>();
        }

        public IEnumerable<IStratisApp> HostedApps => this.hostedApps.Select(x => x.app);

        public void Host(IEnumerable<IStratisApp> stratisApps)
        {
            stratisApps.Where(x => x.IsSinglePageApp).ToList().ForEach(this.HostSinglePageApp);
        }

        public void Close() => this.Dispose();

        public void Dispose()
        {
            this.hostedApps.ForEach(x => x.host.Dispose());
            this.hostedApps.Clear();
        }

        private void HostSinglePageApp(IStratisApp stratisApp)
        {
            try
            {
                int[] nextFreePort = { 0 };
                IpHelper.FindPorts(nextFreePort);
                stratisApp.Address = $"http://localhost:{nextFreePort.First()}";

                (IStratisApp app, IWebHost host) pair = (stratisApp, new WebHostBuilder()
                            .UseKestrel()
                            .UseIISIntegration()
                            .UseWebRoot(Path.Combine(stratisApp.Location, stratisApp.WebRoot))
                            .UseContentRoot(Directory.GetCurrentDirectory())
                            .UseUrls(stratisApp.Address)
                            .UseStartup<SinglePageStartup>()
                            .Build());

                pair.host.Start();                

                this.hostedApps.Add(pair);                
                this.logger.LogInformation("SPA '{0}' hosted at {1}", stratisApp.DisplayName, stratisApp.Address);
            }
            catch (Exception e)
            {
                this.logger.LogError("Failed to host app '{0}' :{1}", stratisApp.DisplayName, e.Message);
            }
        }
    }
}
