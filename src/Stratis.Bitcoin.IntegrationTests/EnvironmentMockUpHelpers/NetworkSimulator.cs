using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.EnvironmentMockUpHelpers
{
    public class NetworkSimulator : IDisposable
    {
        public List<CoreNode> Nodes { get; private set; }

        private NodeBuilder nodeBuilder;

        public NetworkSimulator()
        {
            this.nodeBuilder = NodeBuilder.Create();
        }

        public void Initialize(int nodesCount)
        {
            this.Nodes = new List<CoreNode>();

            //create nodes
            for (int i = 0; i < nodesCount; ++i)
            {
                CoreNode node = this.nodeBuilder.CreateStratisPowNode(true);
                node.NotInIBD();

                node.FullNode.WalletManager().CreateWallet("dummyPassword", "dummyWallet");
                var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("dummyWallet", "account 0"));
                var wallet = node.FullNode.WalletManager().GetWalletByName("dummyWallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("dummyPassword", address).PrivateKey;
                node.SetDummyMinerSecret(new BitcoinSecret(key, node.FullNode.Network));

                this.Nodes.Add(node);
            }
        }

        public bool AreAllNodesAtSameHeight()
        {
            var height = this.Nodes.First().FullNode.Chain.Height;

            foreach (var node in this.Nodes)
            {
                if (node.FullNode.Chain.Height != height)
                    return false;
            }
            return true;
        }

        public void Dispose()
        {
            this.nodeBuilder.Dispose();
        }

        public bool DidAllNodesReachHeight(int height)
        {
            foreach (var node in this.Nodes)
            {
                if (node.FullNode.Chain.Height < height)
                    return false;
            }
            return true;
        }

        public void MakeSureEachNodeCanMineAndSync()
        {
            foreach (var node in Nodes)
            {
                Thread.Sleep(1000);
                var currentHeight = node.FullNode.Chain.Height;

                node.GenerateStratis(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node));
                TestHelper.WaitLoop(new Func<bool>(delegate { return node.FullNode.Chain.Height == currentHeight + 1; }));
                TestHelper.WaitLoop(new Func<bool>(delegate { return AreAllNodesAtSameHeight(); }));
            }
        }
    }
}
