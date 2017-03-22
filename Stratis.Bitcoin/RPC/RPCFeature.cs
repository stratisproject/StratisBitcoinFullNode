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
                fullNode.RPCHost = new WebHostBuilder()
                .UseKestrel()
                .ForFullNode(fullNode)
                .UseUrls(this.nodeSettings.RPC.GetUrls())
                .UseIISIntegration()
                .UseStartup<RPC.Startup>()
                .Build();
                fullNode.RPCHost.Start();
                fullNode.Resources.Add(fullNode.RPCHost);
                Logs.RPC.LogInformation("RPC Server listening on: " + Environment.NewLine + String.Join(Environment.NewLine, this.nodeSettings.RPC.GetUrls()));
            }
        }
    }

    public static class RPCBuilderExtension
    {
        public static IFullNodeBuilder UseRPC(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<RPCFeature>()
                .FeatureServices(services =>
                    {
                        services.AddSingleton<FullNode>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}