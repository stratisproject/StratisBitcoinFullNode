using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerConnectorTests
    {
        [Fact]
        public void PeerConnector_FindPeerToConnectTo_Returns_AddNodePeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback, PeerIntroductionType.Add);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback, PeerIntroductionType.Discover);

            var connector = new PeerConnector(peerAddressManager, PeerIntroductionType.Add);
            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressAddNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnector_FindPeerToConnectTo_Returns_ConnectNodePeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback, PeerIntroductionType.Add);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback, PeerIntroductionType.Connect);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback, PeerIntroductionType.Discover);

            var connector = new PeerConnector(peerAddressManager, PeerIntroductionType.Connect);
            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressConnectNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnector_FindPeerToConnectTo_Returns_DiscoveredPeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback, PeerIntroductionType.Add);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback, PeerIntroductionType.Discover);

            var connector = new PeerConnector(peerAddressManager, PeerIntroductionType.Discover);
            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressDiscoverNode.Endpoint, peer.Endpoint);
        }
    }
}
