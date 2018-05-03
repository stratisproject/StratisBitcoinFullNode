using System;
using System.Diagnostics;
using System.IO;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;

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

        public void Start(string dataDir)
        {
            this.process = Process.Start(new FileInfo(this.bitcoinD).FullName, $"-conf=bitcoin.conf -datadir={dataDir} -debug=net");
        }
    }

    public abstract class NodeRunner : INodeRunner
    {
        internal readonly Action<IFullNodeBuilder> Callback;
        public bool IsDisposed => this.FullNode.State == FullNodeState.Disposed;
        public FullNode FullNode { get; set; }

        protected NodeRunner(Action<IFullNodeBuilder> callback = null)
        {
            this.Callback = callback;
        }

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public abstract void Start(string dataDir);

        protected void StartWithCallBack(NodeSettings settings)
        {
            var builder = new FullNodeBuilder().UseNodeSettings(settings);
            this.Callback(builder);
            this.FullNode = (FullNode)builder.Build();
        }
    }

    public sealed class StratisBitcoinPosRunner : NodeRunner
    {
        public StratisBitcoinPosRunner(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void Start(string dataDir)
        {
            var nodeSettings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false);
            BuildFullNode(nodeSettings);
            this.FullNode.Start();
        }

        public void BuildFullNode(NodeSettings settings)
        {
            if (this.Callback != null)
                StartWithCallBack(settings);
            else
            {
                this.FullNode = (FullNode)new FullNodeBuilder()
                        .UseNodeSettings(settings)
                        .UsePosConsensus()
                        .UseBlockStore()
                        .UseMempool()
                        .UseWallet()
                        .AddPowPosMining()
                        .AddRPC()
                        .MockIBD()
                        .SubstituteDateTimeProviderFor<MiningFeature>()
                        .Build();
            }
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

    public sealed class StratisPosApiRunner : NodeRunner
    {
        public StratisPosApiRunner(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void Start(string dataDir)
        {
            var nodeSettings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false);
            BuildFullNode(nodeSettings);
            this.FullNode.Start();
        }

        public void BuildFullNode(NodeSettings nodeSettings)
        {
            if (this.Callback != null)
                StartWithCallBack(nodeSettings);
            else
            {
                this.FullNode = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddPowPosMining()
                    .UseWallet()
                    .UseApi()
                    .AddRPC()
                    .Build();
            }
        }
    }

    public sealed class StratisBitcoinPowRunner : NodeRunner
    {
        public StratisBitcoinPowRunner(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void Start(string dataDir)
        {
            var nodeSettings = new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + dataDir }, loadConfiguration: false);
            BuildFullNode(nodeSettings);
            this.FullNode.Start();
        }

        public void BuildFullNode(NodeSettings nodeSettings)
        {
            if (this.Callback != null)
                StartWithCallBack(nodeSettings);
            else
            {
                this.FullNode = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePowConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddMining()
                    .UseWallet()
                    .AddRPC()
                    .MockIBD()
                    .Build();
            }
        }
    }

    public sealed class StratisProofOfWorkMiningNode : NodeRunner
    {
        public StratisProofOfWorkMiningNode(Action<IFullNodeBuilder> callback = null)
            : base(callback)
        {
        }

        public override void Start(string dataDir)
        {
            var nodeSettings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + dataDir }, loadConfiguration: false);
            BuildFullNode(nodeSettings);
            this.FullNode.Start();
        }

        public void BuildFullNode(NodeSettings nodeSettings)
        {
            if (this.Callback != null)
                StartWithCallBack(nodeSettings);
            else
            {
                this.FullNode = (FullNode)new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UsePosConsensus()
                    .UseBlockStore()
                    .UseMempool()
                    .AddMining()
                    .UseWallet()
                    .AddRPC()
                    .MockIBD()
                    .SubstituteDateTimeProviderFor<MiningFeature>()
                    .Build();
            }
        }
    }
}