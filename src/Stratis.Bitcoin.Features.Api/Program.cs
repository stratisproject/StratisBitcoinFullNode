using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
        }

        public static IWebHost Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode, ApiSettings apiSettings)
        {
            Guard.NotNull(fullNode, nameof(fullNode));

            Uri apiUri = apiSettings.ApiUri;

            IWebHost host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseUrls(apiUri.ToString())
                .ConfigureServices(collection =>
                {
                    if (services == null)
                    {
                        return;
                    }

                    // copies all the services defined for the full node to the Api.
                    // also copies over singleton instances already defined
                    foreach (ServiceDescriptor service in services)
                    {
                        object obj = fullNode.Services.ServiceProvider.GetService(service.ServiceType);
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

            return host;
        }
    }
}
