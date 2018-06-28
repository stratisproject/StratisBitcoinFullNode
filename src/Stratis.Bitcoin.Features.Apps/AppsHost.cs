using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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

    public class AppsHost : IAppsHost
    {
        private int seedPort = 32500;
        private readonly List<IWebHost> hosts = new List<IWebHost>();                

        public bool Host(IEnumerable<IStratisApp> stratisApps)
        {
            this.hosts.AddRange(stratisApps.Select(CreateHost));    
            this.hosts.ForEach(x => x.Start());       

            return true;
        }

        private IWebHost CreateHost(IStratisApp stratisApp)
        {
            return new WebHostBuilder()
                .UseKestrel()
                .UseIISIntegration()
                .UseWebRoot(stratisApp.WebRoot)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseUrls($"http://localhost:{this.seedPort++}")
                .UseStartup<Startup>()
                .Build();
        }
    }
}
