using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
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

            X509Certificate2 certificate = apiSettings.UseHttps ? GetHttpsCertificate(apiSettings, store) : null;

            webHostBuilder = webHostBuilder ?? new WebHostBuilder();

            webHostBuilder
                .UseKestrel(options =>
                    {
                        if (!apiSettings.UseHttps) return;
                        options.Listen(
                            IPAddress.Loopback,
                            apiSettings.ApiPort,
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

        private static Action<KestrelServerOptions> GetKestrelConfigurationAction(ApiSettings apiSettings, ICertificateStore store)
        {
            if (!apiSettings.UseHttps) return _ => { };

            X509Certificate2 certificate = GetHttpsCertificate(apiSettings, store);
            return options =>
                {
                    options.Listen(
                        IPAddress.Loopback,
                        apiSettings.ApiPort,
                        listenOptions => { listenOptions.UseHttps(certificate); });
                };
        }

        private static X509Certificate2 GetHttpsCertificate(ApiSettings apiSettings, ICertificateStore store)
        {
            var certificateFileName = apiSettings.HttpsCertificateFileName;

            if (store.TryGet(certificateFileName, out var certificate))
                return certificate;

            using (var securePasswordString = store.PasswordReader.ReadSecurePassword("Please enter a password for the new self signed certificate."))
            {
                certificate = store.BuildSelfSignedServerCertificate(securePasswordString);
                store.Save(certificate, apiSettings.HttpsCertificateFileName, securePasswordString);
            }

            return certificate;
        }

        private static readonly char[] charsForPasswords = Enumerable.Range(char.MinValue, char.MaxValue)
                                                                .Select(x => (char)x)
                                                                .Where(c => !char.IsControl(c))
                                                                .ToArray();

        public static SecureString BuildRandomSecureString()
        {
            var secureString = new SecureString();
            using (var rngCryptoServiceProvider = new RNGCryptoServiceProvider())
            {
                var thisInt = new byte[sizeof(Int32)];
                rngCryptoServiceProvider.GetBytes(thisInt);
                Enumerable.Range(0,32)
                    .Select(_ => charsForPasswords[BitConverter.ToInt32(thisInt, 0) % charsForPasswords.Length])
                    .ToList().ForEach(c => secureString.AppendChar(c));
            }
            secureString.MakeReadOnly();
            return secureString;
        }
    }
}
