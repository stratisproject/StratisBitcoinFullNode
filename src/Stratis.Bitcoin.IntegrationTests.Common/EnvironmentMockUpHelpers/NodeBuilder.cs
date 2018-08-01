using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.IntegrationTests.Common.Runners;
using Stratis.Bitcoin.Tests.Common;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
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

        private CoreNode CreateNode(NodeRunner runner, bool start, string configFile = "bitcoin.conf", bool useCookieAuth = false)
        {
            var node = new CoreNode(runner, this, configFile, useCookieAuth);
            this.Nodes.Add(node);
            if (start) node.Start();
            return node;
        }

        public CoreNode CreateBitcoinCoreNode(string version = "0.13.1", bool useCookieAuth = false)
        {
            string bitcoinDPath = GetBitcoinCorePath(version);
            return CreateNode(new BitcoinCoreRunner(this.GetNextDataFolderName(), bitcoinDPath), start: false, useCookieAuth: useCookieAuth);
        }

        public CoreNode CreateStratisPowNode(bool start = false)
        {
            return CreateNode(new StratisBitcoinPowRunner(this.GetNextDataFolderName()), start);
        }

        public CoreNode CreateStratisCustomPowNode(NodeConfigParameters configParameters, bool start = false)
        {
            var callback = new Action<IFullNodeBuilder>(builder => builder
               .UseBlockStore()
               .UsePowConsensus()
               .UseMempool()
               .AddMining()
               .UseWallet()
               .AddRPC()
               .MockIBD());

            return CreateCustomNode(start, callback, KnownNetworks.RegTest, ProtocolVersion.PROTOCOL_VERSION, configParameters: configParameters);
        }

        public CoreNode CreateStratisPowApiNode(bool start = false)
        {
            return CreateNode(new StratisBitcoinPowApiRunner(this.GetNextDataFolderName()), start);
        }

        public CoreNode CreateStratisPosNode()
        {
            return CreateNode(new StratisBitcoinPosRunner(this.GetNextDataFolderName()), false, "stratis.conf");
        }

        public CoreNode CreateStratisPosApiNode()
        {
            return CreateNode(new StratisPosApiRunner(this.GetNextDataFolderName()), false, "stratis.conf");
        }

        public CoreNode CreateSmartContractNode()
        {
            return CreateNode(new StratisSmartContractNode(this.GetNextDataFolderName()), false, "stratis.conf");
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(new StratisBitcoinPowRunner(cloneNode.FullNode.Settings.DataFolder.RootPath), this, "bitcoin.conf");
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        /// <summary>A helper method to create a node instance with a non-standard set of features enabled. The node can be PoW or PoS, as long as the appropriate features are provided.</summary>
        /// <param name="callback">A callback accepting an instance of <see cref="IFullNodeBuilder"/> that constructs a node with a custom feature set.</param>
        /// <param name="network">The network the node will be running on.</param>
        /// <param name="protocolVersion">Use <see cref="ProtocolVersion.PROTOCOL_VERSION"/> for BTC PoW-like networks and <see cref="ProtocolVersion.ALT_PROTOCOL_VERSION"/> for Stratis PoS-like networks.</param>
        /// <param name="agent">A user agent string to distinguish different node versions from each other.</param>
        /// <param name="configParameters">Use this to pass in any custom configuration parameters used to set up the CoreNode</param>
        public CoreNode CreateCustomNode(bool start, Action<IFullNodeBuilder> callback, Network network, ProtocolVersion protocolVersion = ProtocolVersion.PROTOCOL_VERSION, string agent = "Custom", NodeConfigParameters configParameters = null)
        {
            configParameters = configParameters ?? new NodeConfigParameters();

            configParameters.SetDefaultValueIfUndefined("conf", "custom.conf");
            string configFileName = configParameters["conf"];

            configParameters.SetDefaultValueIfUndefined("datadir", this.GetNextDataFolderName(agent));
            string dataDir = configParameters["datadir"];

            configParameters.ToList().ForEach(p => this.ConfigParameters[p.Key] = p.Value);
            return CreateNode(new CustomNodeRunner(dataDir, callback, network, protocolVersion, configParameters, agent), start, configFileName);
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