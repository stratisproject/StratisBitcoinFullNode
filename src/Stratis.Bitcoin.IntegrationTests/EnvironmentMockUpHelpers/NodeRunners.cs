using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;

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

        public bool IsDisposed
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public FullNode FullNode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        bool INodeRunner.IsDisposed => throw new NotImplementedException();

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

        void INodeRunner.Kill()
        {
            throw new NotImplementedException();
        }

        void INodeRunner.OnStart(string dataDir)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class NodeRunner : INodeRunner
    {
        protected Action<IFullNodeBuilder> Callback;
        public FullNode FullNode { get; set; }
        public bool IsDisposed { get { return this.FullNode.State == FullNodeState.Disposed; } }
        public bool SkipRules { get; internal set; }

        protected NodeRunner(bool skipRules, Action<IFullNodeBuilder> callback = null)
        {
            this.Callback = callback;
            this.SkipRules = skipRules;
        }

        public abstract FullNode OnBuild(NodeSettings nodeSettings);
        public abstract void OnStart(string dataDirectory);

        protected IFullNodeBuilder Build(NodeSettings nodeSettings)
        {
            return new FullNodeBuilder().UseNodeSettings(nodeSettings).UseBlockStore().UseMempool().UseWallet().AddRPC().MockIBD();
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
        public StratisPosRunner(bool skipRules, Action<IFullNodeBuilder> callback = null)
            : base(skipRules, callback)
        {
        }

        public override void OnStart(string dataDir)
        {
            base.Start(new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false));
        }

        public override FullNode OnBuild(NodeSettings nodeSettings)
        {
            FullNode node;

            if (this.Callback != null)
                node = base.BuildFromCallBack(nodeSettings);
            else
            {
                node = (FullNode)Build(nodeSettings)
                                .UsePosConsensus(this.SkipRules ? new PosTestRuleRegistration() : null)
                                .AddPowPosMining()
                                .Build();
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

    public sealed class StratisBitcoinPowRunner : NodeRunner
    {
        public StratisBitcoinPowRunner(bool skipRules, Action<IFullNodeBuilder> callback = null)
            : base(skipRules, callback)
        {
        }

        public override void OnStart(string dataDir)
        {
            base.Start(new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + dataDir }, loadConfiguration: false));
        }

        public override FullNode OnBuild(NodeSettings args)
        {
            FullNode node;

            if (this.Callback != null)
                node = base.BuildFromCallBack(args);
            else
            {
                node = (FullNode)Build(args)
                                .UsePowConsensus(this.SkipRules ? new PowTestRuleRegistration() : null)
                                .AddMining()
                                .Build();
            }

            return node;
        }
    }

    public sealed class StratisPowRunner : NodeRunner
    {
        public StratisPowRunner(bool skipRules, Action<IFullNodeBuilder> callback = null)
            : base(skipRules, callback)
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
            {
                fullNode = (FullNode)Build(nodeSettings)
                                    .UsePowConsensus(this.SkipRules ? new PowTestRuleRegistration() : null)
                                    .AddMining()
                                    .Build();
            }

            return fullNode;
        }
    }

    public sealed class PowTestRuleRegistration : IRuleRegistration
    {
        public IEnumerable<ConsensusRule> GetRules()
        {
            return new List<ConsensusRule>() { new PowTestRule() };
        }
    }

    public sealed class PosTestRuleRegistration : IRuleRegistration
    {
        public IEnumerable<ConsensusRule> GetRules()
        {
            return new List<ConsensusRule>() { new PosTestRule() };
        }
    }

    public sealed class PowTestRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(NetworkOptions.TemporaryOptions), context.ConsensusTip);
            context.BlockValidationContext.ChainedBlock = this.Parent.Chain.GetBlock(context.BlockValidationContext.ChainedBlock.HashBlock) ?? context.BlockValidationContext.ChainedBlock;
            context.SetBestBlock(this.Parent.DateTimeProvider.GetTimeOffset());

            context.Flags = new Base.Deployments.DeploymentFlags();

            return Task.CompletedTask;
        }
    }

    public sealed class PosTestRule : ConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            context.BlockValidationContext.ChainedBlock = new ChainedBlock(context.BlockValidationContext.Block.Header, context.BlockValidationContext.Block.Header.GetHash(NetworkOptions.TemporaryOptions), context.ConsensusTip);
            context.BlockValidationContext.ChainedBlock = this.Parent.Chain.GetBlock(context.BlockValidationContext.ChainedBlock.HashBlock) ?? context.BlockValidationContext.ChainedBlock;
            context.SetBestBlock(this.Parent.DateTimeProvider.GetTimeOffset());

            context.Flags = new Base.Deployments.DeploymentFlags();

            context.SetStake();

            return Task.CompletedTask;
        }
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