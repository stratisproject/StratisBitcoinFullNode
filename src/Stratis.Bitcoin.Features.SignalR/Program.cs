using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SignalR
{
    public class Program
    {
        public static IWebHost Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode,
            SignalRSettings settings, IWebHostBuilder webHostBuilder)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(webHostBuilder, nameof(webHostBuilder));

            Uri uri = settings.SignalRUri;

            webHostBuilder
                .UseKestrel(o => o.AllowSynchronousIO = true)
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseUrls(uri.ToString())
                .ConfigureServices(collection =>
                {
                    if (services == null) return;

                    // copies all the services defined for the full node to the Api.
                    // also copies over singleton instances already defined
                    foreach (ServiceDescriptor service in services)
                    {
                        // open types can't be singletons
                        if (service.ServiceType.IsGenericType || service.Lifetime == ServiceLifetime.Scoped)
                        {
                            collection.Add(service);
                            continue;
                        }

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
                .UseStartup<Startup>();

            IWebHost host = webHostBuilder.Build();

            host.Start();

            return host;
        }
    }
}
