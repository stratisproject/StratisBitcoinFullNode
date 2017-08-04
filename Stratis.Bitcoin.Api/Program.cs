using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin;

namespace Stratis.Bitcoin.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
        }

        public static void Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode)
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseUrls(fullNode.Settings.ApiUri.ToString())
                .ConfigureServices(collection =>
                {
                    if (services == null || fullNode == null)
                    {
                        return;
                    }

                    // copies all the services defined for the full node to the Api.
                    // also copies over singleton instances already defined
                    foreach (var service in services)
                    {
                        var obj = fullNode.Services.ServiceProvider.GetService(service.ServiceType);
                        if (obj != null && service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance == null)
                        {
                            collection.AddSingleton(service.ServiceType, obj);
                        }
                        else
                        {
                            collection.Add(service);
                        }
                    }
                })
                .UseStartup<Startup>()
                .Build();

            host.Start();
        }
    }
}
