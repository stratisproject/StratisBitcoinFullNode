using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            rpcSettings.Load(nodeSettings);
            this.rpcSettings = rpcSettings;
        }

        public override void Start()
        {
            if (this.rpcSettings.Server)
            {
                // TODO: The web host wants to create IServiceProvider, so build (but not start)
                // earlier, if you want to use dependency injection elsewhere
                this.fullNode.RPCHost = new WebHostBuilder()
                .UseKestrel()
                .ForFullNode(this.fullNode)
                .UseUrls(this.rpcSettings.GetUrls())
                .UseIISIntegration()
                .ConfigureServices(collection =>
                {
                    if (this.fullNodeBuilder != null && this.fullNodeBuilder.Services != null && this.fullNode != null)
                    {
                        // copies all the services defined for the full node to the Api.
                        // also copies over singleton instances already defined
                        foreach (var service in this.fullNodeBuilder.Services)
                        {
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
                .UseStartup<RPC.Startup>()
                .Build();

                this.fullNode.RPCHost.Start();
                this.fullNode.Resources.Add(this.fullNode.RPCHost);
                this.logger.LogInformation("RPC Server listening on: " + Environment.NewLine + string.Join(Environment.NewLine, this.rpcSettings.GetUrls()));
            }
            else
            {
                this.logger.LogWarning("RPC Server is off based on configuration.");
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderRPCExtension
    {
        public static IFullNodeBuilder AddRPC(this IFullNodeBuilder fullNodeBuilder, Action<RpcSettings> setup = null)
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
                service.AddSingleton<RpcSettings>(new RpcSettings(setup));
                service.AddSingleton<RPCController>();
            });

            return fullNodeBuilder;
        }
    }
}