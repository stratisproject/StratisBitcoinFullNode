using System;
using System.IO;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerSelectorTests : TestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;

        public PeerSelectorTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();
        }

        [Fact]
        public void PeerState_AllConnectionDataGetsReset()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            addressManager.PeerConnected(networkAddress.Endpoint, DateTime.UtcNow);

            addressManager.PeerAttempted(networkAddress.Endpoint, DateTime.UtcNow);
            addressManager.PeerAttempted(networkAddress.Endpoint, DateTime.UtcNow);
            addressManager.PeerAttempted(networkAddress.Endpoint, DateTime.UtcNow);

            addressManager.PeerConnected(networkAddress.Endpoint, DateTime.UtcNow);

            var peerOne = addressManager.FindPeer(networkAddress.Endpoint);

            Assert.Equal(0, peerOne.ConnectionAttempts);
            Assert.Null(peerOne.LastConnectionAttempt);
            Assert.NotNull(peerOne.LastConnectionSuccess);
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the fresh peers
        /// set.
        ///
        /// Scenario 1:
        /// Peer 1 has had no connection attempts.
        /// Peer 2 has had no connection attempts.
        /// Peer 3 has had no connection attempts.
        /// 
        /// Result:
        /// All 3 peers are in the Fresh set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerFreshSet()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(networkAddressOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressThree, IPAddress.Loopback);

            var peers = peerAddressManager.Peers.Fresh();
            Assert.Equal(3, peers.Count());
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set.
        ///
        /// Scenario:
        /// Peer 1 has had a connection attempt (in the last 60 seconds).
        /// Peer 2 has had a connection attempt (more than 60 seconds ago).
        /// Peer 3 has had a connection attempt (more than 60 seconds ago).
        /// 
        /// Result:
        /// Peers 2 and 3 are in the attempted set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_Scenario1()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(networkAddressOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressThree, IPAddress.Loopback);

            peerAddressManager.PeerAttempted(networkAddressOne.Endpoint, DateTime.UtcNow);
            peerAddressManager.PeerAttempted(networkAddressTwo.Endpoint, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerAttempted(networkAddressThree.Endpoint, DateTime.UtcNow.AddSeconds(-80));

            var peers = peerAddressManager.Peers.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.NetworkAddress.Endpoint.Match(networkAddressOne.Endpoint));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set.
        ///
        /// Scenario:
        /// Peer 1 has had a connection attempt (more than 60 seconds ago).
        /// Peer 2 has had a connection attempt (more than 60 seconds ago).
        /// Peer 3 was attempted unsuccessfully more than 10 times.
        /// 
        /// Result:
        /// Peers 1 and 2 are in the attempted set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_Scenario2()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(networkAddressOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressThree, IPAddress.Loopback);

            peerAddressManager.PeerAttempted(networkAddressOne.Endpoint, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerAttempted(networkAddressTwo.Endpoint, DateTime.UtcNow.AddSeconds(-80));

            for (int i = 0; i < 15; i++)
            {
                peerAddressManager.PeerAttempted(networkAddressThree.Endpoint, DateTime.UtcNow);
            }

            var peers = peerAddressManager.Peers.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.NetworkAddress.Endpoint.Match(networkAddressThree.Endpoint));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the connected peers
        /// set.
        ///
        /// Scenario 1:
        /// Peer 1 has had a successful connection made to it (in the last 60 seconds).
        /// Peer 2 has had a successful connection made to it (more than 60 seconds ago).
        /// Peer 3 has only had an unsuccessful connection attempt.
        /// 
        /// Result:
        /// Peer 2 gets returned in the Connected set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerConnectedSet()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(networkAddressOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressThree, IPAddress.Loopback);

            peerAddressManager.PeerConnected(networkAddressOne.Endpoint, DateTime.UtcNow);
            peerAddressManager.PeerConnected(networkAddressTwo.Endpoint, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerAttempted(networkAddressThree.Endpoint, DateTime.UtcNow);

            var peers = peerAddressManager.Peers.Connected();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.NetworkAddress.Endpoint.Match(networkAddressTwo.Endpoint));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the handshaked peers
        /// set.
        ///
        /// Scenario 1:
        /// Peer 1 has had a successful handshake (in the last 60 seconds).
        /// Peer 2 has had a successful handshake (more than 60 seconds ago).
        /// Peer 3 has only had an unsuccessful connection attempt.
        /// 
        /// Result:
        /// Peer 2 gets returned in the Connected set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerHandshakedSet()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var networkAddressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var networkAddressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(networkAddressOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(networkAddressThree, IPAddress.Loopback);

            peerAddressManager.PeerHandshaked(networkAddressOne.Endpoint, DateTime.UtcNow);
            peerAddressManager.PeerHandshaked(networkAddressTwo.Endpoint, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerAttempted(networkAddressThree.Endpoint, DateTime.UtcNow);

            var peers = peerAddressManager.Peers.Handshaked();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.NetworkAddress.Endpoint.Match(networkAddressTwo.Endpoint));
        }

    }
}
