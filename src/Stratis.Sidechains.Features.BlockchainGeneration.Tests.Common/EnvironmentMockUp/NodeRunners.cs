using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Interfaces;
using Stratis.Sidechains.Features.BlockchainGeneration;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp
{
    public interface INodeRunner
    {
        bool IsDisposed { get; }

        void Kill();

        void Start(Network network, string dataDir);
    }

    public class StratisBitcoinPosRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;

        private string agent = null;

        public StratisBitcoinPosRunner(string agent, Action<IFullNodeBuilder> callback = null)
        {
            this.callback = callback;
            this.agent = agent;
        }

        public bool IsDisposed {
            get { return this.FullNode.State == FullNodeState.Disposed; }
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start(Network network, string dataDir)
        {
            string confArg;
            if (network == Network.StratisMain || network == Network.StratisTest || network == Network.StratisRegTest)
                confArg = "-conf=stratis.conf";
            else
                confArg = $"-conf={SidechainIdentifier.Instance.Name}.conf";

            NodeSettings nodeSettings = agent == null 
                    ? new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { confArg, "-datadir=" + dataDir })
                    : new NodeSettings(network, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { confArg, "-datadir=" + dataDir }, agent:this.agent);
            var node = BuildFullNode(nodeSettings, this.callback);
            this.FullNode = node;
            this.FullNode.Start();
        }

        public static FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
        {
            FullNode node;
            if (callback != null)
            {
                var builder = new FullNodeBuilder().UseNodeSettings(args);
                callback(builder);
                node = (FullNode)builder.Build();
            }
            else
            {
                node = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .MockIBD()
                    .Build();
            }
            return node;
        }

        public FullNode FullNode;
    }

    public static class FullNodeTestBuilderExtension
    {
        public static IFullNodeBuilder MockIBD(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                foreach (IFeatureRegistration feature in features.FeatureRegistrations)
                {
                    feature.FeatureServices(services =>
                    {
                        // Get default IBD implementation and replace it with the mock.
                        ServiceDescriptor ibdService = services.FirstOrDefault(x => x.ServiceType == typeof(IInitialBlockDownloadState));
                        services.AddSingleton<IInitialBlockDownloadState, SimpleInitialBlockDownloadState>();
                    });
                }
            });
            return fullNodeBuilder;
        }
    }
}