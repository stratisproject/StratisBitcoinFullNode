using System.Net;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public sealed class NodeControllerApiTests
    {
        [Fact]
        public async Task Can_BanAndDisconnect_Peer_From_Api_Async()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var nodeA = builder.CreateStratisPosNode(network).Start();

                var nodeBIp = "127.0.0.2";
                var nodeBIpAddress = IPAddress.Parse(nodeBIp);

                var nodeBConfig = new NodeConfigParameters
                {
                    { "-externalip", nodeBIp }
                };

                var nodeB = builder.CreateStratisPosNode(network, configParameters: nodeBConfig).Start();

                var nodeAaddressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                nodeAaddressManager.AddPeer(new IPEndPoint(nodeBIpAddress, nodeB.Endpoint.Port), IPAddress.Loopback);

                TestHelper.Connect(nodeA, nodeB);

                var banPeerModel = new BanPeerViewModel()
                {
                    BanDurationSeconds = 100,
                    PeerAddress = nodeBIp
                };

                await $"http://localhost:{nodeA.ApiPort}/api".AppendPathSegment("node/banpeer").PostJsonAsync(banPeerModel);

                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(nodeA, nodeB));

                var nodeBEndPoint = new IPEndPoint(nodeBIpAddress, nodeB.Endpoint.Port);

                var addressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                var peer = nodeAaddressManager.FindPeer(nodeBEndPoint);

                var peerBanning = nodeA.FullNode.NodeService<IPeerBanning>();
                Assert.True(peerBanning.IsBanned(nodeBEndPoint));
            }
        }

        [Fact]
        public async Task Can_UnBan_Peer_From_Api_Async()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var nodeA = builder.CreateStratisPosNode(network).Start();

                var nodeB_Ip = "127.0.0.2";
                var nodeB_IpAddress = IPAddress.Parse(nodeB_Ip);
                var nodeB_EndPoint = new IPEndPoint(nodeB_IpAddress, 0);

                var nodeAaddressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                nodeAaddressManager.AddPeer(new IPEndPoint(nodeB_IpAddress, 0), IPAddress.Loopback);

                var peerBanning = nodeA.FullNode.NodeService<IPeerBanning>();
                peerBanning.BanAndDisconnectPeer(nodeB_EndPoint);
                Assert.True(peerBanning.IsBanned(nodeB_EndPoint));

                var unBanPeerModel = new UnBanPeerViewModel()
                {
                    PeerAddress = nodeB_Ip
                };

                await $"http://localhost:{nodeA.ApiPort}/api".AppendPathSegment("node/unbanpeer").PostJsonAsync(unBanPeerModel);

                Assert.False(peerBanning.IsBanned(nodeB_EndPoint));
            }
        }
    }
}
