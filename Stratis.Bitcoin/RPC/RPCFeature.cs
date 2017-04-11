﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.RPC.Controllers;
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
                this.fullNode.RPCHost = new WebHostBuilder()
                .UseLoggerFactory(Logs.LoggerFactory)
                .UseKestrel()
                .ForFullNode(this.fullNode)
                .UseUrls(this.nodeSettings.RPC.GetUrls())
                .UseIISIntegration()
                .UseStartup<RPC.Startup>()
                .Build();
                // TODO: use .ConfigureServices() to configure non-ASP.NET services
                // TODO: grab RPCHost.Services to use as IServiceProvider elsewhere
                this.fullNode.RPCHost.Start();
                this.fullNode.Resources.Add(this.fullNode.RPCHost);
                Logs.RPC.LogInformation("RPC Server listening on: " + Environment.NewLine + string.Join(Environment.NewLine, this.nodeSettings.RPC.GetUrls()));
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

            fullNodeBuilder.ConfigureServices(service =>
            {
                service.AddSingleton<FullNodeController>();
                service.AddSingleton<ConnectionManagerController>();
                service.AddSingleton<ConsensusController>();
                service.AddSingleton<MempoolController>();
           });

            return fullNodeBuilder;
        }
    }
}