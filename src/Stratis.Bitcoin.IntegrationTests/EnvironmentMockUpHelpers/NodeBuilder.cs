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
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    internal static class FullNodeExt
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

        public static ChainedBlock HighestPersistedBlock(this FullNode fullNode)
        {
            return fullNode.NodeService<IBlockRepository>().HighestPersistedBlock;
        }
    }

    public enum CoreNodeState
    {
        Stopped,
        Starting,
        Running,
        Killed
    }

    public interface INodeRunner
    {
        FullNode FullNode { get; set; }

        bool IsDisposed { get; }

        void Kill();

        void Start(string dataDir);
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
        public string BitcoinD { get; }

        public List<CoreNode> Nodes { get; }

        public NodeConfigParameters ConfigParameters { get; }

        private int last;
        private string root;

        public NodeBuilder(string root, string bitcoindPath)
        {
            this.last = 0;
            this.Nodes = new List<CoreNode>();
            this.ConfigParameters = new NodeConfigParameters();

            this.root = root;
            this.BitcoinD = bitcoindPath;
        }

        public static NodeBuilder Create([CallerMemberName] string caller = null, string version = "0.13.1")
        {
            KillAnyBitcoinInstances();
            caller = Path.Combine("TestData", caller);
            CreateTestFolder(caller);
            return new NodeBuilder(caller, EnsureDownloaded(version));
        }

        public void SyncNodes()
        {
            foreach (var node in this.Nodes)
            {
                foreach (var node2 in this.Nodes)
                {
                    if (node != node2)
                        node.Sync(node2, true);
                }
            }
        }

        private static string EnsureDownloaded(string version)
        {
            //is a file
            if (version.Length >= 2 && version[1] == ':')
            {
                return version;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var bitcoind = string.Format("TestData/bitcoin-{0}/bin/bitcoind.exe", version);
                if (File.Exists(bitcoind))
                    return bitcoind;
                var zip = string.Format("TestData/bitcoin-{0}-win32.zip", version);
                string url = string.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);
                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(10.0)
                };

                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, data);
                ZipFile.ExtractToDirectory(zip, new FileInfo(zip).Directory.FullName);
                return bitcoind;
            }
            else
            {
                string bitcoind = string.Format("TestData/bitcoin-{0}/bin/bitcoind", version);
                if (File.Exists(bitcoind))
                    return bitcoind;

                var zip = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? string.Format("TestData/bitcoin-{0}-x86_64-linux-gnu.tar.gz", version)
                    : string.Format("TestData/bitcoin-{0}-osx64.tar.gz", version);

                string url = string.Format("https://bitcoin.org/bin/bitcoin-core-{0}/" + Path.GetFileName(zip), version);

                var client = new HttpClient
                {
                    Timeout = TimeSpan.FromMinutes(10.0)
                };

                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, data);
                Process.Start("tar", "-zxvf " + zip + " -C TestData");
                return bitcoind;
            }
        }

        public CoreNode CreateNode(bool start = false)
        {
            var node = new CoreNode(this.GetNextDataFolderName(), new BitcoinCoreRunner(this.BitcoinD), this, Network.RegTest);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPowNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            var node = new CoreNode(this.GetNextDataFolderName(), new StratisBitcoinPowRunner(callback), this, Network.RegTest);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPowMiningNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            var node = new CoreNode(this.GetNextDataFolderName(), new StratisProofOfWorkMiningNode(callback), this, Network.RegTest, configfile: "stratis.conf");
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPosNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            var node = new CoreNode(this.GetNextDataFolderName(), new StratisBitcoinPosRunner(callback), this, Network.RegTest, configfile: "stratis.conf");
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPosApiNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            var node = new CoreNode(this.GetNextDataFolderName(), new StratisPosApiRunner(callback), this, Network.RegTest, configfile: "stratis.conf");
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(cloneNode.Folder, new StratisBitcoinPowRunner(), this, Network.RegTest);
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        private string GetNextDataFolderName()
        {
            var dataFolderName = Path.Combine(this.root, this.last.ToString());
            this.last++;
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
                var applicableBitcoinDProcesses = bitcoinDProcesses.Where(b => b.MainModule.FileName.Contains("Stratis.Bitcoin.IntegrationTests"));
                if (!applicableBitcoinDProcesses.Any())
                    break;

                foreach (var process in applicableBitcoinDProcesses)
                {
                    process.Kill();
                    Thread.Sleep(1000);
                }
            }
        }

        internal static void CreateTestFolder(string folderName)
        {
            var deleteAttempts = 0;
            while (deleteAttempts < 50)
            {
                if (Directory.Exists(folderName))
                {
                    try
                    {
                        Directory.Delete(folderName, true);
                        break;
                    }
                    catch
                    {
                        deleteAttempts++;
                        Thread.Sleep(200);
                    }
                }
                else
                    break;
            }

            if (deleteAttempts >= 50)
                throw new Exception(string.Format("The test folder: {0} could not be created.", folderName));

            Directory.CreateDirectory(folderName);
        }

        internal static void CreateDataFolder(string dataFolder)
        {
            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);
        }
    }
}