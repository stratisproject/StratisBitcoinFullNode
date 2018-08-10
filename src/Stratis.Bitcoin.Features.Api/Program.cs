using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Api
{
    public class Program
    {
        public static IWebHost Initialize(IEnumerable<ServiceDescriptor> services, FullNode fullNode,
            ApiSettings apiSettings, ICertificateStore store, IWebHostBuilder webHostBuilder = null)
        {
            Guard.NotNull(fullNode, nameof(fullNode));

            Uri apiUri = apiSettings.ApiUri;

            X509Certificate2 certificate = GetHttpsCertificate(apiSettings, store);

            webHostBuilder = webHostBuilder ?? new WebHostBuilder();

            webHostBuilder
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, apiSettings.ApiPort,
                        listenOptions => { listenOptions.UseHttps(certificate); });
                })
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
                        if (obj != null && service.Lifetime == ServiceLifetime.Singleton &&
                            service.ImplementationInstance == null)
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

            var host = webHostBuilder.Build();
                

            host.Start();
           
            return host;
        }

        private static X509Certificate2 GetHttpsCertificate(ApiSettings apiSettings, ICertificateStore store)
        {
            var certificateSubjectName = apiSettings.HttpsCertificateSubjectName;

            if (store.TryGet(certificateSubjectName, out var certificate))
                return certificate;

            if (certificateSubjectName != ApiSettings.DefaultCertificateSubjectName)
                throw new FileNotFoundException("Unable to find certificate with name: " + certificateSubjectName);
            

            certificate = store.BuildSelfSignedServerCertificate(certificateSubjectName, Guid.NewGuid().ToString());
            store.Add(certificate);

            return certificate;
        }
    }
}
