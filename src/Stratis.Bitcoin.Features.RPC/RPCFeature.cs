using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.RPC.Controllers;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCFeature : FullNodeFeature
    {
        private readonly FullNode fullNode;

        private readonly NodeSettings nodeSettings;

        private readonly ILogger logger;

        private readonly IFullNodeBuilder fullNodeBuilder;

        private readonly RpcSettings rpcSettings;

        public RPCFeature(IFullNodeBuilder fullNodeBuilder, FullNode fullNode, NodeSettings nodeSettings, ILoggerFactory loggerFactory, RpcSettings rpcSettings)
        {
            this.fullNodeBuilder = fullNodeBuilder;
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.rpcSettings = rpcSettings;
        }

        /// <summary>
        /// Prints command-line help.
        /// </summary>
        /// <param name="network">The network to extract values from.</param>
        public static void PrintHelp(Network network)
        {
            RpcSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####RPC Settings####");
            builder.AppendLine("#Activate RPC Server (default: 0)");
            builder.AppendLine("#server=0");
            builder.AppendLine("#Where the RPC Server binds (default: 127.0.0.1 and ::1)");
            builder.AppendLine("#rpcbind=127.0.0.1");
            builder.AppendLine("#Ip address allowed to connect to RPC (default all: 0.0.0.0 and ::)");
            builder.AppendLine("#rpcallowip=127.0.0.1");
        }

        public override Task InitializeAsync()
        {
            if (this.rpcSettings.Server)
            {
                // TODO: The web host wants to create IServiceProvider, so build (but not start)
                // earlier, if you want to use dependency injection elsewhere
                var webHostBuilder = new WebHostBuilder()
                .UseKestrel()
                .ForFullNode(this.fullNode)
                .UseIISIntegration()
                .ConfigureServices(collection =>
                {
                    if (this.fullNodeBuilder != null && this.fullNodeBuilder.Services != null && this.fullNode != null)
                    {
                        // copies all the services defined for the full node to the Api.
                        // also copies over singleton instances already defined
                        foreach (ServiceDescriptor service in this.fullNodeBuilder.Services)
                        {
                            // open types can't be singletons
                            if (service.ServiceType.IsGenericType || service.Lifetime == ServiceLifetime.Scoped)
                            {
                                collection.Add(service);
                                continue;
                            }

                            object obj = this.fullNode.Services.ServiceProvider.GetService(service.ServiceType);

                            if (obj != null && service.Lifetime == ServiceLifetime.Singleton && service.ImplementationInstance == null)
                            {
                                collection.AddSingleton(service.ServiceType, obj);
                            }
                            else
                            {
                                collection.Add(service);
                            }
                        }
                    }
                })
                .UseStartup<Startup>();

                bool retry = this.rpcSettings.RPCPort == 0;
                int retryCnt = retry ? 10 : 1;

                while (retryCnt-- >= 0)
                {
                    try
                    {
                        if (retry)
                            this.rpcSettings.SetPort(IpHelper.FindPort());

                        IWebHost host = webHostBuilder.UseUrls(this.rpcSettings.GetUrls()).Build();

                        host.Start();

                        this.fullNode.RPCHost = host;

                        break;
                    }
                    catch (IOException err) when (retryCnt != 0 && err.InnerException.GetType() == typeof(AddressInUseException))
                    {
                        continue;
                    }
                }

                this.logger.LogInformation("RPC Server listening on: " + Environment.NewLine + string.Join(Environment.NewLine, this.rpcSettings.GetUrls()));
            }
            else
            {
                this.logger.LogInformation("RPC Server is off based on configuration.");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderRPCExtension
    {
        public static IFullNodeBuilder AddRPC(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<RPCFeature>("rpc");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<RPCFeature>()
                .DependOn<ConsensusFeature>()
                .FeatureServices(services => services.AddSingleton(fullNodeBuilder));
            });

            fullNodeBuilder.ConfigureServices(service =>
            {
                service.AddSingleton<FullNodeController>();
                service.AddSingleton<ConnectionManagerController>();
                service.AddSingleton<RpcSettings>();
                service.AddSingleton<IRPCClientFactory, RPCClientFactory>();
                service.AddSingleton<RPCController>();
            });

            return fullNodeBuilder;
        }
    }
}
