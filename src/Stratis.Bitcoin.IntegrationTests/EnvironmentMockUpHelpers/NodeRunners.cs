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
using Stratis.Bitcoin.Features.Api;
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
        public FullNode FullNode { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public BitcoinCoreRunner(string bitcoinD)
        {
            this.bitcoinD = bitcoinD;
        }

        private Process process;

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

        public void Start(string dataDir, bool mineCoinsFast)
        {
            this.process = Process.Start(new FileInfo(this.bitcoinD).FullName, $"-conf=bitcoin.conf -datadir={dataDir} -debug=net");
        }
    }

    public class StratisBitcoinPosRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;
        public FullNode FullNode { get; set; }
        private bool mineCoinsFast;

        public StratisBitcoinPosRunner(Action<IFullNodeBuilder> callback = null)
        {
            this.callback = callback;
        }

        public bool IsDisposed
        {
            get { return this.FullNode.State == FullNodeState.Disposed; }
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start(string dataDir, bool mineCoinsFast)
        {
            NodeSettings nodeSettings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false);

            this.mineCoinsFast = mineCoinsFast;

            var node = BuildFullNode(nodeSettings, this.callback);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
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
                var builder = new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .UseWallet()
                    .AddPowPosMining()
                    .AddRPC()
                    .MockIBD();

                if (this.mineCoinsFast)
                    builder.SubstituteDateTimeProviderFor<MiningFeature>(new FastCoinsDateTimeProvider());

                node = (FullNode)builder.Build();
            }

            return node;
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
            NodeSettings nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dir}", $"-stake={(staking ? 1 : 0)}", "-walletname=dummy", "-walletpassword=dummy" }, loadConfiguration: false);
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

    public class StratisPosApiRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;
        public FullNode FullNode { get; set; }
        private bool mineCoinsFast;

        public StratisPosApiRunner(Action<IFullNodeBuilder> callback = null)
        {
            this.callback = callback;
        }

        public bool IsDisposed
        {
            get { return this.FullNode.State == FullNodeState.Disposed; }
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start(string dataDir, bool mineCoinsFast)
        {
            NodeSettings nodeSettings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false);

            this.mineCoinsFast = mineCoinsFast;

            var node = BuildFullNode(nodeSettings, this.callback);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
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
                var builder = new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddPowPosMining()
                    .UseWallet()
                    .UseApi()
                    .AddRPC();

                if (this.mineCoinsFast)
                    builder.SubstituteDateTimeProviderFor<MiningFeature>(new FastCoinsDateTimeProvider());

                node = (FullNode)builder.Build();
            }

            return node;
        }
    }

    public sealed class StratisBitcoinPowRunner : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;
        public FullNode FullNode { get; set; }
        private bool mineCoinsFast;

        public StratisBitcoinPowRunner(Action<IFullNodeBuilder> callback = null) : base()
        {
            this.callback = callback;
        }

        public bool IsDisposed
        {
            get { return this.FullNode.State == FullNodeState.Disposed; }
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start(string dataDir, bool mineCoinsFast)
        {
            NodeSettings nodeSettings = new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + dataDir }, loadConfiguration: false);

            this.mineCoinsFast = mineCoinsFast;

            var node = BuildFullNode(nodeSettings, this.callback);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
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
                    .UsePowConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddMining()
                    .UseWallet()
                    .AddRPC()
                    .MockIBD()
                    .Build();
            }

            return node;
        }
    }

    public sealed class StratisProofOfWorkMiningNode : INodeRunner
    {
        private Action<IFullNodeBuilder> callback;
        public FullNode FullNode { get; set; }
        private bool mineCoinsFast;

        public StratisProofOfWorkMiningNode(Action<IFullNodeBuilder> callback = null) : base()
        {
            this.callback = callback;
        }

        public bool IsDisposed
        {
            get { return this.FullNode.State == FullNodeState.Disposed; }
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start(string dataDir, bool mineCoinsFast)
        {
            NodeSettings nodeSettings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false);

            this.mineCoinsFast = mineCoinsFast;

            var node = BuildFullNode(nodeSettings, this.callback);

            this.FullNode = node;
            this.FullNode.Start();
        }

        public FullNode BuildFullNode(NodeSettings args, Action<IFullNodeBuilder> callback = null)
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
                var builder = new FullNodeBuilder()
                    .UseNodeSettings(args)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddMining()
                    .UseWallet()
                    .AddRPC()
                    .MockIBD();

                if (this.mineCoinsFast)
                    builder.SubstituteDateTimeProviderFor<MiningFeature>(new FastCoinsDateTimeProvider());

                node = (FullNode)builder.Build();
            }

            return node;
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
                        {
                            services.Remove(ibdService);
                            services.AddSingleton<IInitialBlockDownloadState, InitialBlockDownloadStateMock>();
                        }
                    });
                }
            });

            return fullNodeBuilder;
        }
    }
}
