using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.IntegrationTests.Common
{
    public class TestHelper
    {
        public static void WaitLoop(Func<bool> act)
        {
            var cancel = new CancellationTokenSource(Debugger.IsAttached ? 15 * 60 * 1000 : 30 * 1000);
            while (!act())
            {
                cancel.Token.ThrowIfCancellationRequested();
                Thread.Sleep(50);
            }
        }

        public static bool AreNodesSynced(CoreNode node1, CoreNode node2)
        {
            if (node1.FullNode.Chain.Tip.HashBlock != node2.FullNode.Chain.Tip.HashBlock) return false;
            if (node1.FullNode.ChainBehaviorState.ConsensusTip.HashBlock != node2.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) return false;
            if (node1.FullNode.GetBlockStoreTip().HashBlock != node2.FullNode.GetBlockStoreTip().HashBlock) return false;
            if (node1.FullNode.MempoolManager().InfoAll().Count != node2.FullNode.MempoolManager().InfoAll().Count) return false;
            if (node1.FullNode.WalletManager().WalletTipHash != node2.FullNode.WalletManager().WalletTipHash) return false;
            if (node1.CreateRPCClient().GetBestBlockHash() != node2.CreateRPCClient().GetBestBlockHash()) return false;
            return true;
        }

        public static bool IsNodeSynced(CoreNode node)
        {
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.ChainBehaviorState.ConsensusTip.HashBlock) return false;
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.GetBlockStoreTip().HashBlock) return false;
            if (node.FullNode.Chain.Tip.HashBlock != node.FullNode.WalletManager().WalletTipHash) return false;
            return true;
        }

        public static void TriggerSync(CoreNode node)
        {
            foreach (INetworkPeer connectedPeer in node.FullNode.ConnectionManager.ConnectedPeers)
                connectedPeer.Behavior<ChainHeadersBehavior>().TrySyncAsync().GetAwaiter().GetResult();
        }

        public static bool IsNodeConnected(CoreNode node)
        {
            if (node.FullNode.ConnectionManager.ConnectedPeers.Any()) return true;
            return false;
        }

        /// <summary>
        /// Find ports that are free to use.
        /// </summary>
        /// <param name="ports">A list of ports to checked or fill/replace as necessary.</param>
        public static void FindPorts(int[] ports)
        {
            int i = 0;
            while (i < ports.Length)
            {
                uint port = RandomUtils.GetUInt32() % 4000;
                port = port + 10000;
                if (ports.Any(p => p == port))
                    continue;

                try
                {
                    var l = new TcpListener(IPAddress.Loopback, (int)port);
                    l.Start();
                    l.Stop();
                    ports[i] = (int)port;
                    i++;
                }
                catch (SocketException)
                {
                }
            }
        }
    }
}
