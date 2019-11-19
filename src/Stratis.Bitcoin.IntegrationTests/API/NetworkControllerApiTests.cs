using System;
using System.Collections.Generic;
using System.Linq;
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
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public sealed class NetworkControllerApiTests
    {
        [Fact]
        public async Task Can_BanAndDisconnect_Peer_From_ApiAsync()
        {
            using (var builder = NodeBuilder.Create(this))
                
            {
                var network = new StratisRegTest();

                var nodeA = builder.CreateStratisPosNode(network, "nc-1-nodeA").Start();

                var nodeBIp = "127.0.0.2";
                var nodeBIpAddress = IPAddress.Parse(nodeBIp);

                var nodeBConfig = new NodeConfigParameters
                {
                    { "-externalip", nodeBIp }
                };

                var nodeB = builder.CreateStratisPosNode(network, agent: "nc-1-nodeB", configParameters: nodeBConfig).Start();

                var nodeAaddressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                nodeAaddressManager.AddPeer(new IPEndPoint(nodeBIpAddress, nodeB.Endpoint.Port), IPAddress.Loopback);

                TestHelper.Connect(nodeA, nodeB);

                var banPeerModel = new SetBanPeerViewModel()
                {
                    BanDurationSeconds = 100,
                    BanCommand = "add",
                    PeerAddress = nodeBIp
                };

                await $"http://localhost:{nodeA.ApiPort}/api".AppendPathSegment("network/setban").PostJsonAsync(banPeerModel);

                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(nodeA, nodeB));

                var nodeBEndPoint = new IPEndPoint(nodeBIpAddress, nodeB.Endpoint.Port);

                var addressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                var peer = nodeAaddressManager.FindPeer(nodeBEndPoint);

                var peerBanning = nodeA.FullNode.NodeService<IPeerBanning>();
                Assert.True(peerBanning.IsBanned(nodeBEndPoint));
            }
        }

        [Fact]
        public async Task Can_UnBan_Peer_From_ApiAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var nodeA = builder.CreateStratisPosNode(network, "nc-2-nodeA").Start();

                var nodeB_Ip = "127.0.0.2";
                var nodeB_IpAddress = IPAddress.Parse(nodeB_Ip);
                var nodeB_EndPoint = new IPEndPoint(nodeB_IpAddress, 0);

                var nodeAaddressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                nodeAaddressManager.AddPeer(new IPEndPoint(nodeB_IpAddress, 0), IPAddress.Loopback);

                var peerBanning = nodeA.FullNode.NodeService<IPeerBanning>();
                peerBanning.BanAndDisconnectPeer(nodeB_EndPoint);
                Assert.True(peerBanning.IsBanned(nodeB_EndPoint));

                var unBanPeerModel = new SetBanPeerViewModel()
                {
                    BanCommand = "remove",
                    PeerAddress = nodeB_Ip
                };

                await $"http://localhost:{nodeA.ApiPort}/api".AppendPathSegment("network/setban").PostJsonAsync(unBanPeerModel);

                Assert.False(peerBanning.IsBanned(nodeB_EndPoint));
            }
        }

        [Fact]
        public async Task Can_ClearAll_Banned_PeersAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var nodeA = builder.CreateStratisPosNode(network, "nc-3-nodeA").Start();

                var nodeB_Ip = "127.0.0.2";
                var nodeB_IpAddress = IPAddress.Parse(nodeB_Ip);
                var nodeB_EndPoint = new IPEndPoint(nodeB_IpAddress, 0);

                var nodeAaddressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                nodeAaddressManager.AddPeer(new IPEndPoint(nodeB_IpAddress, 0), IPAddress.Loopback);

                var peerBanning = nodeA.FullNode.NodeService<IPeerBanning>();
                peerBanning.BanAndDisconnectPeer(nodeB_EndPoint);
                Assert.True(peerBanning.IsBanned(nodeB_EndPoint));

                await $"http://localhost:{nodeA.ApiPort}/api".AppendPathSegment("network/clearbanned").PostJsonAsync(null);

                Assert.False(peerBanning.IsBanned(nodeB_EndPoint));
            }
        }

        [Fact]
        public async Task Can_GetBannedPeers_From_ApiAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                var nodeA = builder.CreateStratisPosNode(network, "nc-4-nodeA").Start();

                var nodeB_Ip = "127.0.0.2";
                var nodeB_IpAddress = IPAddress.Parse(nodeB_Ip);
                var nodeB_EndPoint = new IPEndPoint(nodeB_IpAddress, 0);

                var nodeB_Ip2 = "127.0.0.3";
                var nodeB_IpAddress2 = IPAddress.Parse(nodeB_Ip2);
                var nodeB_EndPoint2 = new IPEndPoint(nodeB_IpAddress2, 0);

                var nodeAaddressManager = nodeA.FullNode.NodeService<IPeerAddressManager>();
                nodeAaddressManager.AddPeer(new IPEndPoint(nodeB_IpAddress, 0), IPAddress.Loopback);

                var peerBanning = nodeA.FullNode.NodeService<IPeerBanning>();
                peerBanning.BanAndDisconnectPeer(nodeB_EndPoint, "This peer is banned because of a reason");
                Assert.True(peerBanning.IsBanned(nodeB_EndPoint));

                peerBanning.BanAndDisconnectPeer(nodeB_EndPoint2, "This peer is banned because of another reason");
                Assert.True(peerBanning.IsBanned(nodeB_EndPoint2));

                var bannedPeers = await $"http://localhost:{nodeA.ApiPort}/api".AppendPathSegment("network/getbans").GetJsonAsync<List<BannedPeerModel>>();

                Assert.Equal(2, bannedPeers.Count);
                bannedPeers = bannedPeers.OrderBy(b => b.EndPoint).ToList();

                var bannedPeer = bannedPeers[0];
                Assert.Equal("This peer is banned because of a reason", bannedPeer.BanReason);
                Assert.True(DateTime.UtcNow < bannedPeer.BanUntil.Value);
                Assert.Equal("[::ffff:127.0.0.2]:0", bannedPeer.EndPoint);

                bannedPeer = bannedPeers[1];
                Assert.Equal("This peer is banned because of another reason", bannedPeer.BanReason);
                Assert.True(DateTime.UtcNow < bannedPeer.BanUntil.Value);
                Assert.Equal("[::ffff:127.0.0.3]:0", bannedPeer.EndPoint);
            }
        }
    }
}
