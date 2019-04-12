using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    public class Program
    {
        public static IWebHost Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode,
            ApiSettings apiSettings, ICertificateStore store, IWebHostBuilder webHostBuilder)
        {
            Guard.NotNull(fullNode, nameof(fullNode));
            Guard.NotNull(webHostBuilder, nameof(webHostBuilder));

            Uri apiUri = apiSettings.ApiUri;

            X509Certificate2 certificate = apiSettings.UseHttps
                ? GetHttpsCertificate(apiSettings.HttpsCertificateFilePath, store)
                : null;

            webHostBuilder
                .UseKestrel(options =>
                    {
                        if (!apiSettings.UseHttps)
                            return;

                        Action<ListenOptions> configureListener = listenOptions => { listenOptions.UseHttps(certificate); };
                        var ipAddresses = Dns.GetHostAddresses(apiSettings.ApiUri.DnsSafeHost);
                        foreach (var ipAddress in ipAddresses)
                        {
                            options.Listen(ipAddress, apiSettings.ApiPort, configureListener);
                        }
                    })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                //.UseUrls(apiUri.ToString())
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

            bool retry = apiSettings.ApiPort == 0;
            int retryCnt = retry ? 10 : 1;

            while (retryCnt-- >= 0)
            {
                try
                {
                    if (retry)
                        apiSettings.SetPort(IpHelper.FindPort());

                    IWebHost host = webHostBuilder.UseUrls(apiSettings.ApiUri.ToString()).Build();

                    host.Start();

                    return host;
                }
                catch (IOException err) when (retryCnt != 0 && err.InnerException.GetType() == typeof(AddressInUseException))
                {
                    continue;
                }
            }

            // Should never reach here.
            return null;
        }

        private static X509Certificate2 GetHttpsCertificate(string certificateFilePath, ICertificateStore store)
        {
            if (store.TryGet(certificateFilePath, out var certificate))
                return certificate;

            throw new FileLoadException($"Failed to load certificate from path {certificateFilePath}");
        }
    }
}
