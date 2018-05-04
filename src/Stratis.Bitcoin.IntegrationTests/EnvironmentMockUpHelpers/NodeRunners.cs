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
    public class BitcoinCoreRunner : NodeRunner
    {
        private string bitcoinD;

        public BitcoinCoreRunner(string dataDir, string bitcoinD)
            : base(dataDir)
        {
            this.bitcoinD = bitcoinD;
        }

        private Process process;

        public new bool IsDisposed
        {
            get { return this.process == null && this.process.HasExited; }
        }

        public new void Kill()
        {
            if (!this.IsDisposed)
            {
                this.process.Kill();
                this.process.WaitForExit();
            }
        }

        public override void OnStart()
        {
            this.process = Process.Start(new FileInfo(this.bitcoinD).FullName, $"-conf=bitcoin.conf -datadir={this.DataFolder} -debug=net");
        }

        public override void BuildNode()
        {
        }
    }

    public abstract class NodeRunner
    {
        public readonly string DataFolder;
        public bool IsDisposed => this.FullNode.State == FullNodeState.Disposed;
        public FullNode FullNode { get; set; }

        protected NodeRunner(string dataDir)
        {
            this.DataFolder = dataDir;
        }

        public abstract void BuildNode();
        public abstract void OnStart();

        public void Kill()
        {
            this.FullNode?.Dispose();
        }

        public void Start()
        {
            BuildNode();
            OnStart();
        }
    }

    public sealed class StratisBitcoinPosRunner : NodeRunner
    {
        public StratisBitcoinPosRunner(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder }, loadConfiguration: false);

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

        public override void OnStart()
        {
            this.FullNode.Start();
        }

        /// <summary>
        /// Builds a node with POS miner and RPC enabled.
        /// </summary>
        /// <param name="dataDir">Data directory that the node should use.</param>
        /// <returns>Interface to the newly built node.</returns>
        /// <remarks>Currently the node built here does not actually stake as it has no coins in the wallet,
        /// but all the features required for it are enabled.</remarks>
        public static IFullNode BuildStakingNode(string dataDir, bool staking = true)
        {
            var nodeSettings = new NodeSettings(args: new string[] { $"-datadir={dataDir}", $"-stake={(staking ? 1 : 0)}", "-walletname=dummy", "-walletpassword=dummy" }, loadConfiguration: false);
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
        public StratisPosApiRunner(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder }, loadConfiguration: false);

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UsePosConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .AddPowPosMining()
                            .UseWallet()
                            .UseApi()
                            .AddRPC()
                            .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }

    public sealed class StratisBitcoinPowRunner : NodeRunner
    {
        public StratisBitcoinPowRunner(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(args: new string[] { "-conf=bitcoin.conf", "-datadir=" + this.DataFolder }, loadConfiguration: false);

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
                            .UsePowConsensus()
                            .UseBlockStore()
                            .UseMempool()
                            .AddMining()
                            .UseWallet()
                            .AddRPC()
                            .MockIBD()
                            .Build();
        }

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }

    public sealed class StratisProofOfWorkMiningNode : NodeRunner
    {
        public StratisProofOfWorkMiningNode(string dataDir)
            : base(dataDir)
        {
        }

        public override void BuildNode()
        {
            var settings = new NodeSettings(Network.StratisRegTest, ProtocolVersion.ALT_PROTOCOL_VERSION, args: new string[] { "-conf=stratis.conf", "-datadir=" + this.DataFolder }, loadConfiguration: false);

            this.FullNode = (FullNode)new FullNodeBuilder()
                            .UseNodeSettings(settings)
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

        public override void OnStart()
        {
            this.FullNode.Start();
        }
    }
}