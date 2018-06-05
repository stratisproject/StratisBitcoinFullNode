using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp
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

        public static ChainedBlock HighestPersistedBlock(this FullNode fullNode)
        {
            return fullNode.NodeService<IBlockRepository>().HighestPersistedBlock;
        }
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
        public const string BaseTestDataPath = "TestData";

        private int last;
        private string root;
        private List<IDisposable> disposables;

        public NodeConfigParameters ConfigParameters { get; }

        public NodeBuilder(string root)
        {
            this.last = 0;
            this.Nodes = new List<CoreNode>();
            this.disposables = new List<IDisposable>();
            this.root = root;
            this.ConfigParameters = new NodeConfigParameters();
        }

        public static NodeBuilder Create([CallerMemberName] string caller = null)
        {
            Directory.CreateDirectory(BaseTestDataPath);
            caller = Path.Combine(BaseTestDataPath, caller);
            TestUtils.ShellCleanupFolder(caller);
            Directory.CreateDirectory(caller);
            return new NodeBuilder(caller);
        }

        public CoreNode CreateStratisPosNode(bool start = false, Action<IFullNodeBuilder> callback = null, string agent = null)
        {
            string child = this.CreateNewEmptyFolder();
            var node = new CoreNode(child, new StratisBitcoinPosRunner(agent, callback), this, Network.StratisRegTest, configfile: "stratis.conf");
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        public CoreNode CreatePosSidechainNode(string sidechainName, bool start = false, Action<IFullNodeBuilder> callback = null, Action<string, string> setupSidechain = null, string agent = null)
        {
            string child = this.CreateNewEmptyFolder();
            if (setupSidechain == null) setupSidechain = this.SetupSidechainIdentifier;

            //for the sidechains node we must pre-setup the data folder and insert the sidechains.json
            string dataFolder = Path.Combine(child, "data");
            Directory.CreateDirectory(dataFolder);
            setupSidechain(sidechainName, dataFolder);

            //we can't clean folders here because our sidechains.json is already in its folder
            var node = new CoreNode(child, new StratisBitcoinPosRunner(agent, callback), this, SidechainNetwork.SidechainRegTest, configfile:$"{SidechainIdentifier.Instance.Name}.conf", cleanfolders:false);
            this.Nodes.Add(node);
            if (start)
                node.Start();
            return node;
        }

        private void SetupSidechainIdentifier(string sidechainName, string folder)
        {
            Directory.CreateDirectory(folder);
            File.Copy(@"..\..\..\..\..\assets\sidechains.json", Path.Combine(folder, "sidechains.json"));
            var sidechainIdentifier = SidechainIdentifier.Create(sidechainName, folder);
            this.disposables.Add(sidechainIdentifier);
        }

        private string CreateNewEmptyFolder()
        {
            var child = Path.Combine(this.root, this.last.ToString());
            this.last++;

            TestUtils.ShellCleanupFolder(child);

            return child;
        }

        public void Dispose()
        {
            foreach (var node in this.Nodes)
            {
                if(node?.State != CoreNodeState.Stopped && node?.State != CoreNodeState.Killed) node?.Kill();
            }

            foreach (var disposable in this.disposables)
            {
                try {disposable?.Dispose();}
                catch
                {
                    // ignored
                }
            }
        }
    }
}
