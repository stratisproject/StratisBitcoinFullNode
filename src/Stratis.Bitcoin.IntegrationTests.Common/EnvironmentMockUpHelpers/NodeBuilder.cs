using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public static class FullNodeExt
    {
        public static WalletManager WalletManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletManager>() as WalletManager;
        }

        public static WalletTransactionHandler WalletTransactionHandler(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletTransactionHandler>() as WalletTransactionHandler;
        }

        public static ConsensusLoop ConsensusLoop(this FullNode fullNode)
        {
            return fullNode.NodeService<IConsensusLoop>() as ConsensusLoop;
        }

        public static CoinView CoinView(this FullNode fullNode)
        {
            return fullNode.NodeService<CoinView>();
        }

        public static MempoolManager MempoolManager(this FullNode fullNode)
        {
            return fullNode.NodeService<MempoolManager>();
        }

        public static BlockStoreManager BlockStoreManager(this FullNode fullNode)
        {
            return fullNode.NodeService<BlockStoreManager>();
        }

        public static ChainedHeader GetBlockStoreTip(this FullNode fullNode)
        {
            return fullNode.NodeService<IChainState>().BlockStoreTip;
        }
    }

    public enum CoreNodeState
    {
        Stopped,
        Starting,
        Running,
        Killed
    }

    public class NodeConfigParameters : Dictionary<string, string>
    {
        public void Import(NodeConfigParameters configParameters)
        {
            foreach (KeyValuePair<string, string> kv in configParameters)
            {
                if (!this.ContainsKey(kv.Key))
                    this.Add(kv.Key, kv.Value);
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, string> kv in this)
                builder.AppendLine(kv.Key + "=" + kv.Value);
            return builder.ToString();
        }
    }

    public class NodeBuilder : IDisposable
    {
        public List<CoreNode> Nodes { get; }

        public NodeConfigParameters ConfigParameters { get; }

        private int lastDataFolderIndex;

        private string rootFolder;

        public NodeBuilder(string rootFolder)
        {
            this.lastDataFolderIndex = 0;
            this.Nodes = new List<CoreNode>();
            this.ConfigParameters = new NodeConfigParameters();

            this.rootFolder = rootFolder;
        }

        public static NodeBuilder Create(object caller, [CallerMemberName] string callingMethod = null)
        {
            KillAnyBitcoinInstances();
            string testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            return new NodeBuilder(testFolderPath);
        }

        public static NodeBuilder Create(string testDirectory)
        {
            KillAnyBitcoinInstances();
            string testFolderPath = TestBase.CreateTestDir(testDirectory);
            return new NodeBuilder(testFolderPath);
        }

        private static string GetBitcoinCorePath(string version)
        {
            string path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                path = $"../../../../External Libs/Bitcoin Core/{version}/Windows/bitcoind.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                path = $"../../../../External Libs/Bitcoin Core/{version}/Linux/bitcoind";
            else
                path = $"../../../../External Libs/Bitcoin Core/{version}/OSX/bitcoind";

            if (File.Exists(path))
                return path;

            throw new FileNotFoundException($"Could not load the file {path}.");
        }

        private CoreNode CreateNode(NodeRunner runner, bool start, string configFile = "bitcoin.conf")
        {
            var node = new CoreNode(runner, this, configFile);
            this.Nodes.Add(node);
            if (start) node.Start();
            return node;
        }

        public CoreNode CreateBitcoinCoreNode(string version = "0.13.1")
        {
            string bitcoinDPath = GetBitcoinCorePath(version);
            return CreateNode(new BitcoinCoreRunner(this.GetNextDataFolderName(), bitcoinDPath), false);
        }

        public CoreNode CreateStratisPowNode(bool start = false)
        {
            return CreateNode(new StratisBitcoinPowRunner(this.GetNextDataFolderName()), start);
        }

        public CoreNode CreateStratisPosNode()
        {
            return CreateNode(new StratisBitcoinPosRunner(this.GetNextDataFolderName()), false, "stratis.conf");
        }

        public CoreNode CreateStratisPosApiNode()
        {
            return CreateNode(new StratisPosApiRunner(this.GetNextDataFolderName()), false, "stratis.conf");
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(new StratisBitcoinPowRunner(cloneNode.FullNode.Settings.DataFolder.RootPath), this, "bitcoin.conf");
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        /// <summary>A helper method to create a node instance with a non-standard set of features enabled. The node can be PoW or PoS, as long as the appropriate features are provided.</summary>
        /// <param name="dataDir">The node's data directory where downloaded chain data gets stored.</param>
        /// <param name="callback">A callback accepting an instance of <see cref="IFullNodeBuilder"/> that constructs a node with a custom feature set.</param>
        /// <param name="network">The network the node will be running on.</param>
        /// <param name="protocolVersion">Use <see cref="ProtocolVersion.PROTOCOL_VERSION"/> for BTC PoW-like networks and <see cref="ProtocolVersion.ALT_PROTOCOL_VERSION"/> for Stratis PoS-like networks.</param>
        /// <param name="configFileName">The name for the node's configuration file.</param>
        /// <param name="agent">A user agent string to distinguish different node versions from each other.</param>
        public CoreNode CreateCustomNode(bool start, Action<IFullNodeBuilder> callback, Network network, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, IEnumerable<string> args = null, string agent = "Custom")
        {
            var argsList = args as List<string> ?? args?.ToList() ?? new List<string>();

            string configFileName = "custom.conf";
            if (!argsList.Any(a => a.StartsWith("-conf="))) argsList.Add($"-conf={configFileName}");
            else configFileName = argsList.First(a => a.StartsWith("-conf=")).Replace("-conf=", "");

            string dataDir = this.GetNextDataFolderName(agent);
            if (!argsList.Any(a => a.StartsWith("-datadir="))) argsList.Add($"-datadir={dataDir}");

            return CreateNode(new CustomNodeRunner(dataDir, callback, network, protocolVersion, argsList, agent), start, configFileName);
        }

        private string GetNextDataFolderName(string folderName = null)
        {
            var numberedFolderName = string.Join(".",
                new[] { this.lastDataFolderIndex.ToString(), folderName }.Where(s => s != null));
            string dataFolderName = Path.Combine(this.rootFolder, numberedFolderName);
            this.lastDataFolderIndex++;
            return dataFolderName;
        }

        public void StartAll()
        {
            foreach (CoreNode node in this.Nodes.Where(n => n.State == CoreNodeState.Stopped))
            {
                node.Start();
            }
        }

        public void Dispose()
        {
            foreach (CoreNode node in this.Nodes)
                node.Kill();

            KillAnyBitcoinInstances();
        }

        internal static void KillAnyBitcoinInstances()
        {
            while (true)
            {
                Process[] bitcoinDProcesses = Process.GetProcessesByName("bitcoind");
                IEnumerable<Process> applicableBitcoinDProcesses = bitcoinDProcesses.Where(b => b.MainModule.FileName.Contains("External Libs"));
                if (!applicableBitcoinDProcesses.Any())
                    break;

                foreach (Process process in applicableBitcoinDProcesses)
                {
                    process.Kill();
                    Thread.Sleep(1000);
                }
            }
        }
    }
}