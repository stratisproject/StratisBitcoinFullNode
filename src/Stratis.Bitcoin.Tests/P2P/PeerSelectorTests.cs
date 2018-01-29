using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities.Extensions;
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

            var peers = peerAddressManager.PeerSelector.Fresh();
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

            var peers = peerAddressManager.PeerSelector.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.EndPoint.Match(networkAddressOne.Endpoint));
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

            var peers = peerAddressManager.PeerSelector.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.EndPoint.Match(networkAddressThree.Endpoint));
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

            var peers = peerAddressManager.PeerSelector.Connected();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.EndPoint.Match(networkAddressTwo.Endpoint));
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

            peerAddressManager.PeerConnected(networkAddressOne.Endpoint, DateTime.UtcNow);
            peerAddressManager.PeerHandshaked(networkAddressOne.Endpoint, DateTime.UtcNow);

            peerAddressManager.PeerConnected(networkAddressTwo.Endpoint, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerHandshaked(networkAddressTwo.Endpoint, DateTime.UtcNow.AddSeconds(-80));

            peerAddressManager.PeerAttempted(networkAddressThree.Endpoint, DateTime.UtcNow);

            var peers = peerAddressManager.PeerSelector.Handshaked();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.EndPoint.Match(networkAddressTwo.Endpoint));
        }

        /// <summary>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        ///
        /// Scenario 1:
        /// PeerAddressManager contains 15 peers.
        /// 7 Peers = Handshaked.
        /// 8 Peers = Fresh.
        /// 
        /// We ask for 8 peers.
        /// 
        /// Result:
        /// 4 handshaked peers returned.
        /// 4 fresh peers returned.
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario1()
        {
            var peersToAdd = new List<NetworkAddress>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new NetworkAddress(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerConnected(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddSeconds(-80));
                peerAddressManager.PeerHandshaked(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddSeconds(-80));
            }

            var peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(8);
            Assert.Equal(8, peers.Count());
            Assert.Equal(4, peers.Count(p => p.Handshaked));
            Assert.Equal(4, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        ///
        /// Scenario 2:
        /// PeerAddressManager contains 15 peers.
        /// 7 Peers = Handshaked.
        /// 8 Peers = Fresh.
        /// 
        /// We ask for 15 peers.
        /// 
        /// Result:
        /// 7 handshaked peers returned.
        /// 8 fresh peers returned.
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario2()
        {
            var peersToAdd = new List<NetworkAddress>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new NetworkAddress(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerConnected(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddSeconds(-80));
                peerAddressManager.PeerHandshaked(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddSeconds(-80));
            }

            var peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(15);
            Assert.Equal(15, peers.Count());
            Assert.Equal(7, peers.Count(p => p.Handshaked));
            Assert.Equal(8, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        ///
        /// Scenario 3:
        /// PeerAddressManager contains 15 peers.
        /// 
        /// 7 Peers = Attempted.
        /// 8 Peers = Fresh.
        /// 
        /// We ask for 15 peers.
        /// 
        /// Result:
        /// 7 attempted peers returned.
        /// 8 fresh peers returned.
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario3()
        {
            var peersToAdd = new List<NetworkAddress>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new NetworkAddress(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerAttempted(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddSeconds(-80));
            }

            var peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(15);
            Assert.Equal(15, peers.Count());
            Assert.Equal(7, peers.Count(p => p.Attempted));
            Assert.Equal(8, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        ///
        /// Scenario 4:
        /// PeerAddressManager contains 15 peers.
        /// 
        /// 2 Peers = Connected
        /// 13 Peers = Fresh
        /// 
        /// We ask for 15 peers.
        /// 
        /// Result:
        /// 2 connected peers returned.
        /// 13 fresh peers returned.
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario4()
        {
            var peersToAdd = new List<NetworkAddress>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new NetworkAddress(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 2; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerConnected(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddSeconds(-80));
            }

            var peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(15);
            Assert.Equal(15, peers.Count());
            Assert.Equal(2, peers.Count(p => p.Connected));
            Assert.Equal(13, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// Tests how peers are returned from the selector during discovery.
        ///
        /// Scenario 1:
        /// PeerAddressManager contains 5 peers.
        /// 3 Peers was recently discovered from in the last 24 hours.
        /// 2 Peers was discovered from more than 24 hours ago.
        /// 
        /// We ask for 5 peers.
        /// 
        /// Result:
        /// 2 peers returned.
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForDiscovery_Scenario1()
        {
            var peersToAdd = new List<NetworkAddress>();

            for (int i = 1; i <= 5; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new NetworkAddress(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            //These peers were all discovered from in the last 24 hours
            for (int i = 1; i <= 3; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerDiscoveredFrom(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddHours(-5));
            }

            //These peers were all discovered from more than 24 hours ago
            for (int i = 4; i <= 5; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerDiscoveredFrom(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddHours(-25));
            }

            var peers = peerAddressManager.PeerSelector.SelectPeersForDiscovery(5);
            Assert.Equal(2, peers.Count());
        }
    }
}