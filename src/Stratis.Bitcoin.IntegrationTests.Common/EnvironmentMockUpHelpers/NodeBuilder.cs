using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
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
            foreach (var kv in configParameters)
            {
                if (!this.ContainsKey(kv.Key))
                    this.Add(kv.Key, kv.Value);
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var kv in this)
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
            var testFolderPath = TestBase.CreateTestDir(caller, callingMethod);
            return new NodeBuilder(testFolderPath);
        }

        public static NodeBuilder Create(string testDirectory)
        {
            KillAnyBitcoinInstances();
            var testFolderPath = TestBase.CreateTestDir(testDirectory);
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

        private CoreNode CreateNode(NodeRunner runner, Network network, bool start, string configFile = "bitcoin.conf")
        {
            var node = new CoreNode(runner, this, network, configFile);
            this.Nodes.Add(node);
            if (start) node.Start();
            return node;
        }

        public CoreNode CreateBitcoinCoreNode(bool start = false, string version = "0.13.1")
        {
            string bitcoinDPath = GetBitcoinCorePath(version);
            return CreateNode(new BitcoinCoreRunner(this.GetNextDataFolderName(), bitcoinDPath), Network.RegTest, start);
        }

        public CoreNode CreateStratisPowNode(bool start = false)
        {
            return CreateNode(new StratisBitcoinPowRunner(this.GetNextDataFolderName()), Network.RegTest, start);
        }

        public CoreNode CreateStratisPowMiningNode(bool start = false)
        {
            return CreateNode(new StratisProofOfWorkMiningNode(this.GetNextDataFolderName()), Network.RegTest, start, "stratis.conf");
        }

        public CoreNode CreateStratisPosNode(bool start = false)
        {
            return CreateNode(new StratisBitcoinPosRunner(this.GetNextDataFolderName()), Network.RegTest, start, "stratis.conf");
        }

        public CoreNode CreateStratisPosApiNode(bool start = false)
        {
            return CreateNode(new StratisPosApiRunner(this.GetNextDataFolderName()), Network.RegTest, start, "stratis.conf");
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(new StratisBitcoinPowRunner(cloneNode.FullNode.Settings.DataFolder.RootPath), this, Network.RegTest, "bitcoin.conf");
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        private string GetNextDataFolderName()
        {
            var dataFolderName = Path.Combine(this.rootFolder, this.lastDataFolderIndex.ToString());
            this.lastDataFolderIndex++;
            return dataFolderName;
        }

        public void StartAll()
        {
            foreach (var node in this.Nodes.Where(n => n.State == CoreNodeState.Stopped))
            {
                node.Start();
            }
        }

        public void Dispose()
        {
            foreach (var node in this.Nodes)
                node.Kill();

            KillAnyBitcoinInstances();
        }

        internal static void KillAnyBitcoinInstances()
        {
            while (true)
            {
                var bitcoinDProcesses = Process.GetProcessesByName("bitcoind");
                var applicableBitcoinDProcesses = bitcoinDProcesses.Where(b => b.MainModule.FileName.Contains("External Libs"));
                if (!applicableBitcoinDProcesses.Any())
                    break;

                foreach (var process in applicableBitcoinDProcesses)
                {
                    process.Kill();
                    Thread.Sleep(1000);
                }
            }
        }
    }
}