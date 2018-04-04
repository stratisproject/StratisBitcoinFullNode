using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class BitcoinCoreRunner : INodeRunner
    {
        private string bitcoinD;

        public BitcoinCoreRunner(string bitcoinD)
        {
            this.bitcoinD = bitcoinD;
        }

        private Process process;

        public FullNode FullNode
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public bool IsDisposed
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public void Kill()
        {
            if (!this.IsDisposed)
            {
                this.process.Kill();
                this.process.WaitForExit();
            }
        }

        public void OnStart(string dataDir)
        {
            this.process = Process.Start(new FileInfo(this.bitcoinD).FullName, $"-conf=bitcoin.conf -datadir={dataDir} -debug=net");
        }
    }

    public abstract class NodeRunner : INodeRunner
    {
        protected Action<IFullNodeBuilder> Callback;
        public FullNode FullNode { get; set; }
        public bool IsDisposed { get { return this.FullNode.State == FullNodeState.Disposed; } }

        protected NodeRunner(Action<IFullNodeBuilder> callback = null)
        {
            this.Callback = callback;
        }

        public abstract FullNode OnBuild(NodeSettings nodeSettings);
        public abstract void OnStart(string dataDirectory);

        protected IFullNodeBuilder Build(NodeSettings nodeSettings)
        {
            return new FullNodeBuilder().UseNodeSettings(nodeSettings).UseBlockStore().UseMempool().UseWallet().AddRPC();
        }

        protected FullNode BuildFromCallBack(NodeSettings nodeSettings)
        {
            var builder = new FullNodeBuilder().UseNodeSettings(nodeSettings);
            this.Callback(builder);
            return (FullNode)builder.Build();
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        protected void Start(NodeSettings nodeSettings)
        {
            this.FullNode = OnBuild(nodeSettings);
            this.FullNode.Start();
        }
    }

    public sealed class StratisPosRunner : NodeRunner
    {
        public StratisPosRunner(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void OnStart(string dataDir)
        {
            base.Start(new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false));
        }

        public override FullNode OnBuild(NodeSettings nodeSettings)
        {
            FullNode fullNode;

            if (this.Callback != null)
                fullNode = base.BuildFromCallBack(nodeSettings);
            else
                fullNode = (FullNode)Build(nodeSettings).UsePosConsensus().AddPowPosMining().MockIBD().SubstituteDateTimeProviderFor<MiningFeature>(new InstantPowBlockDateTimeProvider()).Build();

            return fullNode;
        }

        /// <summary>
        /// Builds a node with POS miner and RPC enabled.
        /// </summary>
        /// <param name="dir">Data directory that the node should use.</param>
        /// <returns>Interface to the newly built node.</returns>
        /// <remarks>Currently the node built here does not actually stake as it has no coins in the wallet,
        /// but all the features required for it are enabled.</remarks>
        public static IFullNode BuildStakingNode(string dir, bool staking = true)
        {
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dir}", $"-stake={(staking ? 1 : 0)}", "-walletname=dummy", "-walletpassword=dummy" }, loadConfiguration: false);
            var fullNodeBuilder = new FullNodeBuilder(nodeSettings);
            IFullNode fullNode = fullNodeBuilder
                .UsePosConsensus()
                .UseBlockStore()
                .UseMempool()
                .UseWallet()
                .AddPowPosMining()
                .AddRPC()
                .MockIBD()
                .Build();

            return fullNode;
        }
    }

    public sealed class BitcoinPowRunner : NodeRunner
    {
        public BitcoinPowRunner(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void OnStart(string dataDir)
        {
            base.Start(new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + dataDir }, loadConfiguration: false));
        }

        public override FullNode OnBuild(NodeSettings nodeSettings)
        {
            FullNode fullNode;

            if (this.Callback != null)
                fullNode = base.BuildFromCallBack(nodeSettings);
            else
                fullNode = (FullNode)Build(nodeSettings).UsePowConsensus().AddMining().MockIBD().Build();

            return fullNode;
        }
    }

    public sealed class StratisPowRunner : NodeRunner
    {
        public StratisPowRunner(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void OnStart(string dataDir)
        {
            base.Start(new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false));
        }

        public override FullNode OnBuild(NodeSettings nodeSettings)
        {
            FullNode fullNode;

            if (this.Callback != null)
                fullNode = BuildFromCallBack(nodeSettings);
            else
                fullNode = (FullNode)Build(nodeSettings).UsePosConsensus().AddMining().MockIBD().SubstituteDateTimeProviderFor<MiningFeature>(new InstantPowBlockDateTimeProvider()).Build();

            return fullNode;
        }
    }

    public static class FullNodeTestBuilderExtension
    {
        /// <summary>
        /// Substitute the <see cref="IDateTimeProvider"/> for a given feature.
        /// </summary>
        /// <typeparam name="T">The feature to substitute the provider for.</typeparam>
        public static IFullNodeBuilder SubstituteDateTimeProviderFor<T>(this IFullNodeBuilder fullNodeBuilder, IDateTimeProvider provider)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                var feature = features.FeatureRegistrations.FirstOrDefault(f => f.FeatureType == typeof(T));
                if (feature != null)
                {
                    feature.FeatureServices(services =>
                    {
                        ServiceDescriptor service = services.FirstOrDefault(s => s.ServiceType == typeof(IDateTimeProvider));
                        if (service != null)
                            services.Remove(service);
                        services.AddSingleton(provider);
                    });
                }
            });

            return fullNodeBuilder;
        }

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

                        if (ibdService != null)
                            services.Remove(ibdService);

                        services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadStateMock>();
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}