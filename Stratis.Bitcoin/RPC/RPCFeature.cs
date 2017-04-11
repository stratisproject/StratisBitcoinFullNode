using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Logging;
using Microsoft.Extensions.Logging;
using System;

namespace Stratis.Bitcoin.RPC
{
    public class RPCFeature : FullNodeFeature
    {
        private readonly FullNode fullNode;
        private readonly NodeSettings nodeSettings;
        public RPCFeature(FullNode fullNode, NodeSettings nodeSettings)
        {
            this.fullNode = fullNode;
            this.nodeSettings = Guard.NotNull(nodeSettings, nameof(nodeSettings));
        }

        public override void Start()
        {
            if (this.nodeSettings.RPC != null)
            {
                // TODO: The web host wants to create IServiceProvider, so build (but not start) 
                // earlier, if you want to use dependency injection elsewhere
                fullNode.RPCHost = new WebHostBuilder()
                .UseLoggerFactory(Logs.LoggerFactory)
                .UseKestrel()
                .ForFullNode(fullNode)
                .UseUrls(this.nodeSettings.RPC.GetUrls())
                .UseIISIntegration()
                .UseStartup<RPC.Startup>()
                .Build();
                // TODO: use .ConfigureServices() to configure non-ASP.NET services
                // TODO: grab RPCHost.Services to use as IServiceProvider elsewhere
                fullNode.RPCHost.Start();
                fullNode.Resources.Add(fullNode.RPCHost);
                Logs.RPC.LogInformation("RPC Server listening on: " + Environment.NewLine + String.Join(Environment.NewLine, this.nodeSettings.RPC.GetUrls()));
            }
            else
            {
                Logs.RPC.LogWarning("RPC Server is off based on configuration.");
            }
        }
    }

    public static class RPCBuilderExtension
    {
        public static IFullNodeBuilder AddRPC(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<RPCFeature>();
            });

            return fullNodeBuilder;
        }
    }
}