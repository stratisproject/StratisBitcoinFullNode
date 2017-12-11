using System;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerSelectorTests
    {
        [Fact]
        public void PeerSelection_NoConnectionAttempts_ReturnFromFresh()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager();
            peerAddressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var result = peerAddressManager.Peers.Fresh().FirstOrDefault();
            Assert.NotNull(result);
        }

        [Fact]
        public void PeerSelection_HasHadConnectionAttempts_DoesNotReturnFromFresh()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager();
            peerAddressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var peer = peerAddressManager.FindPeer(networkAddress.Endpoint);
            peer.SetAttempted(DateTimeOffset.Now);

            var result = peerAddressManager.Peers.Fresh().FirstOrDefault();
            Assert.Null(result);
        }

        [Fact]
        public void PeerSelection_HasConnected_DoesReturnFromConnected()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager();
            peerAddressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var peer = peerAddressManager.FindPeer(networkAddress.Endpoint);
            peer.SetAttempted(DateTimeOffset.Now);
            peer.SetConnected(DateTimeOffset.Now);

            var result = peerAddressManager.Peers.Connected().FirstOrDefault();
            Assert.NotNull(result);
        }

        [Fact]
        public void PeerSelection_HasConnected_DoesNotReturnFromHandshaked()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager();
            peerAddressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var peer = peerAddressManager.FindPeer(networkAddress.Endpoint);
            peer.SetAttempted(DateTimeOffset.Now);
            peer.SetConnected(DateTimeOffset.Now);

            var result = peerAddressManager.Peers.Handshaked().FirstOrDefault();
            Assert.Null(result);
        }

        [Fact]
        public void PeerSelection_HasAttemptedButNotConnected_DoesNotReturnFromConnected()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager();
            peerAddressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var peer = peerAddressManager.FindPeer(networkAddress.Endpoint);
            peer.SetAttempted(DateTimeOffset.Now);

            var result = peerAddressManager.Peers.Connected().FirstOrDefault();
            Assert.Null(result);
        }

        [Fact]
        public void PeerSelection_Handshaked_DoesReturnFromHandshaked()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerAddressManager = new PeerAddressManager();
            peerAddressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var peer = peerAddressManager.FindPeer(networkAddress.Endpoint);
            peer.SetAttempted(DateTimeOffset.Now);
            peer.SetConnected(DateTimeOffset.Now);
            peer.SetHandshaked(DateTimeOffset.Now);

            var result = peerAddressManager.Peers.Handshaked().FirstOrDefault();
            Assert.NotNull(result);
        }
    }
}