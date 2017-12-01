﻿using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerConnectorTests
    {
        [Fact]
        public void PeerConnectorAddNode_FindPeerToConnectTo_Returns_AddNodePeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };
            nodeSettings.ConnectionManager.AddNode.Add(networkAddressAddNode.Endpoint);
            var connector = new PeerConnectorAddNode(nodeSettings, peerAddressManager);

            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressAddNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnectorConnect_FindPeerToConnectTo_Returns_ConnectNodePeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };
            nodeSettings.ConnectionManager.Connect.Add(networkAddressConnectNode.Endpoint);
            var connector = new PeerConnectorConnectNode(nodeSettings, peerAddressManager);

            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressConnectNode.Endpoint, peer.Endpoint);
        }

        [Fact]
        public void PeerConnectorDiscovery_FindPeerToConnectTo_Returns_DiscoveredPeers()
        {
            var peerAddressManager = new PeerAddressManager();

            var ipAddressOne = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressAddNode = new NetworkAddress(ipAddressOne, 80);

            var ipAddressThree = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressConnectNode = new NetworkAddress(ipAddressThree, 80);

            var ipAddressTwo = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressDiscoverNode = new NetworkAddress(ipAddressTwo, 80);

            peerAddressManager.AddPeer(networkAddressAddNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressConnectNode, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressDiscoverNode, IPAddress.Loopback);

            var nodeSettings = new NodeSettings
            {
                ConnectionManager = new Configuration.Settings.ConnectionManagerSettings()
            };
            nodeSettings.ConnectionManager.AddNode.Add(networkAddressAddNode.Endpoint);
            nodeSettings.ConnectionManager.Connect.Add(networkAddressConnectNode.Endpoint);
            var connector = new PeerConnectorDiscovery(nodeSettings, peerAddressManager);

            var peer = connector.FindPeerToConnectTo();
            Assert.Equal(networkAddressDiscoverNode.Endpoint, peer.Endpoint);
        }
    }
}