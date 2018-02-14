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
using System.Threading.Tasks;
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
        private List<IDisposable> disposables;

        public NodeBuilder(string root, string bitcoindPath)
        {
            this.last = 0;
            this.Nodes = new List<CoreNode>();
            this.ConfigParameters = new NodeConfigParameters();
            this.disposables = new List<IDisposable>();

            this.root = root;
            this.BitcoinD = bitcoindPath;
        }

        /// <summary>
        /// Deletes test folders. Stops "bitcoind" if required.
        /// </summary>
        /// <param name="folder">The folder to remove.</param>
        /// <param name="tryKill">If set to true will try to stop "bitcoind" if running.</param>
        /// <returns>Returns true if the folder was successfully removed and false otherwise.</returns>
        public static bool CleanupTestFolder(string folder, bool tryKill = true)
        {
            for (int retry = 0; retry < 2; retry++)
            {
                try
                {
                    Directory.Delete(folder, true);
                    return true;
                }
                catch (DirectoryNotFoundException)
                {
                    return true;
                }
                catch (Exception)
                {
                }

                if (tryKill)
                {
                    tryKill = false;

                    foreach (var bitcoind in Process.GetProcessesByName("bitcoind"))
                        if (bitcoind.MainModule.FileName.Contains("Stratis.Bitcoin.IntegrationTests"))
                            bitcoind.Kill();

                    Thread.Sleep(1000);
                }
            }

            return false;
        }

        public static NodeBuilder Create([CallerMemberName] string caller = null, string version = "0.13.1")
        {
            Directory.CreateDirectory("TestData");
            var path = EnsureDownloaded(version);
            caller = Path.Combine("TestData", caller);
            CleanupTestFolder(caller);
            Directory.CreateDirectory(caller);
            return new NodeBuilder(caller, path);
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
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10.0);
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
                HttpClient client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(10.0);
                var data = client.GetByteArrayAsync(url).GetAwaiter().GetResult();
                File.WriteAllBytes(zip, data);
                Process.Start("tar", "-zxvf " + zip + " -C TestData");
                return bitcoind;
            }
        }

        public CoreNode CreateNode(bool start = false)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new BitcoinCoreRunner(this.BitcoinD), this, Network.RegTest);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPowNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new StratisBitcoinPowRunner(callback), this, Network.RegTest);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreateStratisPosNode(bool start = false, Action<IFullNodeBuilder> callback = null)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new StratisBitcoinPosRunner(callback), this, Network.RegTest, configfile: "stratis.conf");
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CloneStratisNode(CoreNode cloneNode)
        {
            var node = new CoreNode(cloneNode.Folder, new StratisBitcoinPowRunner(), this, Network.RegTest, false);
            this.Nodes.Add(node);
            this.Nodes.Remove(cloneNode);
            return node;
        }

        private string CreateNewEmptyFolder()
        {
            var child = Path.Combine(this.root, this.last.ToString());
            this.last++;

            CleanupTestFolder(child);

            return child;
        }

        public void StartAll()
        {
            Task.WaitAll(this.Nodes.Where(n => n.State == CoreNodeState.Stopped).Select(n => n.StartAsync()).ToArray());
        }

        public void Dispose()
        {
            foreach (var node in this.Nodes)
                node.Kill();
            foreach (var disposable in this.disposables)
                disposable.Dispose();
        }

        internal void AddDisposable(IDisposable group)
        {
            this.disposables.Add(group);
        }
    }
}
