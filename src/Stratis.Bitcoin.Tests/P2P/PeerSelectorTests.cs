using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerSelectorTests : LogsTestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;

        public PeerSelectorTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();
        }

        /// <summary>
        /// <para>
        /// Ensures that connection data is reset.
        /// </para><para>
        /// Scenario 1:
        /// Add peer, make some attempts and connect.
        /// Find Peer.
        /// </para><para>
        /// Result:
        /// Connection attepts reset to 0.
        /// Last attempt set to null.
        /// Last connection success date set to a value.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_AllConnectionDataGetsReset()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(endpoint, IPAddress.Loopback);

            peerAddressManager.PeerConnected(endpoint, DateTime.UtcNow);

            peerAddressManager.PeerAttempted(endpoint, DateTime.UtcNow);
            peerAddressManager.PeerAttempted(endpoint, DateTime.UtcNow);
            peerAddressManager.PeerAttempted(endpoint, DateTime.UtcNow);

            peerAddressManager.PeerConnected(endpoint, DateTime.UtcNow);

            PeerAddress peerOne = peerAddressManager.FindPeer(endpoint);

            Assert.Equal(0, peerOne.ConnectionAttempts);
            Assert.Null(peerOne.LastAttempt);
            Assert.NotNull(peerOne.LastConnectionSuccess);
        }

        /// <summary>
        /// <para>
        /// Ensures that a particular peer is returned from the fresh peers
        /// set.
        /// </para><para>
        /// Scenario 1:
        /// Peer 1 has had no connection attempts.
        /// Peer 2 has had no connection attempts.
        /// Peer 3 has had no connection attempts.
        /// </para><para>
        /// Result:
        /// All 3 peers are in the Fresh set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerFreshSet()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Fresh();
            Assert.Equal(3, peers.Count());
        }

        /// <summary>
        /// <para>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set.
        /// </para><para>
        /// Scenario:
        /// Peer 1 has had a connection attempt (in the last 60 seconds).
        /// Peer 2 has had a connection attempt (more than 1 hour ago).
        /// Peer 3 has had a connection attempt (more than 1 hour ago).
        /// </para><para>
        /// Result:
        /// Peers 2 and 3 are in the attempted set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_Scenario1()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerAttempted(endPointOne, DateTime.UtcNow);
            peerAddressManager.PeerAttempted(endPointTwo, DateTime.UtcNow.AddHours(-2));
            peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow.AddHours(-2));

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.Endpoint.Match(endPointOne));
        }

        /// <summary>
        /// <para>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set.
        /// </para><para>
        /// Scenario:
        /// Peer 1 has had a connection attempt (more than 1 hour ago).
        /// Peer 2 has had a connection attempt (more than 1 hour ago).
        /// Peer 3 was attempted unsuccessfully more than 10 times.
        /// </para><para>
        /// Result:
        /// Peers 1 and 2 are in the attempted set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_Scenario2()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerAttempted(endPointOne, DateTime.UtcNow.AddHours(-2));
            peerAddressManager.PeerAttempted(endPointTwo, DateTime.UtcNow.AddHours(-820));

            for (int i = 0; i < 15; i++)
            {
                peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow);
            }

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.Endpoint.Match(endPointThree));
        }

        /// <summary>
        /// <para>
        /// Ensures that a particular peer is returned from the connected peers
        /// set.
        /// </para><para>
        /// Scenario 1:
        /// Peer 1 has had a successful connection made to it (in the last 60 seconds).
        /// Peer 2 has had a successful connection made to it (more than 60 seconds ago).
        /// Peer 3 has only had an unsuccessful connection attempt.
        /// </para><para>
        /// Result:
        /// Peer 2 gets returned in the Connected set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerConnectedSet()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);

            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerConnected(endPointOne, DateTime.UtcNow);
            peerAddressManager.PeerConnected(endPointTwo, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow);

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Connected();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointTwo));
        }

        /// <summary>
        /// <para>
        /// Ensures that a particular peer is returned from the handshaked peers
        /// set.
        /// </para><para>
        /// Scenario 1:
        /// Peer 1 has had a successful handshake (in the last 60 seconds).
        /// Peer 2 has had a successful handshake (more than 60 seconds ago).
        /// Peer 3 has only had an unsuccessful connection attempt.
        /// </para><para>
        /// Result:
        /// Peer 2 gets returned in the Connected set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerHandshakedSet()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);

            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerConnected(endPointOne, DateTime.UtcNow);
            peerAddressManager.PeerHandshaked(endPointOne, DateTime.UtcNow);

            peerAddressManager.PeerConnected(endPointTwo, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerHandshaked(endPointTwo, DateTime.UtcNow.AddSeconds(-80));

            peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow);

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Handshaked();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointTwo));
        }

        /// <summary>
        /// <para>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        /// </para><para>
        /// Scenario 1:
        /// PeerAddressManager contains 15 peers.
        /// 7 Peers = Handshaked.
        /// 8 Peers = Fresh.
        /// </para><para>
        /// We ask for 8 peers.
        /// </para><para>
        /// Result:
        /// 4 handshaked peers returned.
        /// 4 fresh peers returned.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario1()
        {
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
                peerAddressManager.PeerHandshaked(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            }

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(8);
            Assert.Equal(8, peers.Count());
            Assert.Equal(4, peers.Count(p => p.Handshaked));
            Assert.Equal(4, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// <para>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        /// </para><para>
        /// Scenario 2:
        /// PeerAddressManager contains 15 peers.
        /// 7 Peers = Handshaked.
        /// 8 Peers = Fresh.
        /// </para><para>
        /// We ask for 15 peers.
        /// </para><para>
        /// Result:
        /// 7 handshaked peers returned.
        /// 8 fresh peers returned.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario2()
        {
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
                peerAddressManager.PeerHandshaked(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            }

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(15);
            Assert.Equal(15, peers.Count());
            Assert.Equal(7, peers.Count(p => p.Handshaked));
            Assert.Equal(8, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// <para>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        /// </para><para>
        /// Scenario 3:
        /// PeerAddressManager contains 15 peers.
        /// </para><para>
        /// 7 Peers = Attempted.
        /// 8 Peers = Fresh.
        /// </para><para>
        /// We ask for 15 peers.
        /// </para><para>
        /// Result:
        /// 7 attempted peers returned.
        /// 8 fresh peers returned.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario3()
        {
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerAttempted(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddHours(-2));
            }

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(15);
            Assert.Equal(15, peers.Count());
            Assert.Equal(7, peers.Count(p => p.Attempted));
            Assert.Equal(8, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// <para>
        /// Tests how peers are returned from the selector during GetAddrPayload.
        /// </para><para>
        /// Scenario 4:
        /// PeerAddressManager contains 15 peers.
        /// </para><para>
        /// 2 Peers = Connected
        /// 13 Peers = Fresh
        /// </para><para>
        /// We ask for 15 peers.
        /// </para><para>
        /// Result:
        /// 2 connected peers returned.
        /// 13 fresh peers returned.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForGetAddrPayload_Scenario4()
        {
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);

            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 2; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            }

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.SelectPeersForGetAddrPayload(15);
            Assert.Equal(15, peers.Count());
            Assert.Equal(2, peers.Count(p => p.Connected));
            Assert.Equal(13, peers.Count(p => p.Fresh));
        }

        /// <summary>
        /// <para>
        /// Tests how peers are returned from the selector during discovery.
        /// </para><para>
        /// Scenario 1:
        /// PeerAddressManager contains 5 peers.
        /// 3 Peers was recently discovered from in the last 24 hours.
        /// 2 Peers was discovered from more than 24 hours ago.
        /// </para><para>
        /// We ask for 5 peers.
        /// </para><para>
        /// Result:
        /// 2 peers returned.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForDiscovery_Scenario1()
        {
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 5; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);

            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            //These peers were all discovered from in the last 24 hours
            for (int i = 1; i <= 3; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerDiscoveredFrom(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddHours(-5));
            }

            //These peers were all discovered from more than 24 hours ago
            for (int i = 4; i <= 5; i++)
            {
                IPAddress ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerDiscoveredFrom(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddHours(-25));
            }

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.SelectPeersForDiscovery(5);
            Assert.Equal(2, peers.Count());
        }

        /// <summary>
        /// Ensures a banned peer is ignored from the selector during discovery.
        /// <para>
        /// Scenario :
        /// PeerAddressManager contains two peers.
        /// One banned peer.
        /// One discovered peers.
        /// </para>
        /// <para>
        /// Result:
        /// One peer returned, banned peer ignored.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerSelector_ReturnPeersForDiscovery_IgnoringBannedPeer()
        {
            string discoveredpeer = "::ffff:192.168.0.2";

            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse(discoveredpeer);
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);

            // Discovered peer.
            ipAddress = IPAddress.Parse(discoveredpeer);
            peerAddressManager.PeerDiscoveredFrom(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddHours(-25));

            // Banned peer.
            peerAddressManager.FindPeer(endPointOne).BanUntil = DateTime.UtcNow.AddMinutes(1);

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.SelectPeersForDiscovery(2);
            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointTwo));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set and ignores banned peers.
        /// <para>
        /// Scenario:
        /// Peer 1 has had a connection attempt (more than 1 hour ago).
        /// Peer 2 has had a connection attempt (more than 1 hour ago), and is banned.
        /// </para>
        /// <para>
        /// Result:
        /// Peer 1 is in the attempted set, peer 2 is ignored.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_IgnoringBannedPeer()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);

            peerAddressManager.FindPeer(endPointTwo).BanUntil = DateTime.UtcNow.AddMinutes(1);

            peerAddressManager.PeerAttempted(endPointOne, DateTime.UtcNow.AddHours(-2));
            peerAddressManager.PeerAttempted(endPointTwo, DateTime.UtcNow.AddHours(-2));

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Attempted();

            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointOne));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the connected peers
        /// set and ignores banned peers.
        /// <para>
        /// Scenario :
        /// Peer 1 has had a successful connection made to it (more than 60 seconds ago).
        /// Peer 2 has had a successful connection made to it (more than 60 seconds ago), and is banned.
        /// </para>
        /// <para>
        /// Result:
        /// Peer 1 gets returned in the Connected set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerConnectedSet_IgnoringBannedPeer()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);

            peerAddressManager.FindPeer(endPointTwo).BanUntil = DateTime.UtcNow.AddMinutes(1);

            peerAddressManager.PeerConnected(endPointOne, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerConnected(endPointTwo, DateTime.UtcNow.AddSeconds(-80));

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Connected();

            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointOne));
        }

        /// <summary>
        /// Ensures that a peer is returned from the fresh peers
        /// set, and the banned peer is ignored.
        /// <para>
        /// Scenario 1:
        /// Peer 1 has had no connection attempts.
        /// Peer 2 has had no connection attempts, and is banned.
        /// </para>
        /// <para>
        /// Result:
        /// Peer 1 is in the Fresh set.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerFreshSet_IgnoringBannedPeer()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);

            peerAddressManager.FindPeer(endPointTwo).BanUntil = DateTime.UtcNow.AddMinutes(1);

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Fresh();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointOne));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the handshaked peers
        /// set, and the banned peer is ignored.
        /// <para>
        /// Scenario 1:
        /// Peer 1 has had a successful handshake (more than 60 seconds ago).
        /// Peer 2 has had a successful handshake (more than 60 seconds ago), and is banned.
        /// </para>
        /// <para>
        /// Result:
        /// Peer 1 gets returned in the Connected set, and Peer 2 is ignored.
        /// </para>
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerHandshakedSet_IgnoringBanned()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory, new SelfEndpointTracker(this.extendedLoggerFactory));
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);

            peerAddressManager.FindPeer(endPointTwo).BanUntil = DateTime.UtcNow.AddMinutes(1);

            peerAddressManager.PeerConnected(endPointOne, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerHandshaked(endPointOne, DateTime.UtcNow.AddSeconds(-80));

            peerAddressManager.PeerConnected(endPointTwo, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerHandshaked(endPointTwo, DateTime.UtcNow.AddSeconds(-80));

            IEnumerable<PeerAddress> peers = peerAddressManager.PeerSelector.Handshaked();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.Endpoint.Match(endPointOne));
        }

        /// <summary>
        /// Tests that own peer address is not returning during SelectPeersForDiscovery and SelectPeer.
        /// <para>
        /// Scenario 1 - SelectPeersForDiscovery:
        /// PeerAddressManager contains 2 peers.
        /// 1 Peer that is within self endpoint tracker.
        /// 1 Peer not withing self endpoint tracker.
        /// </para><para>
        /// We ask for 2 peers.
        /// </para><para>
        /// Result:
        /// 1 peers returned. It is not the one within self endpoint tracker.
        /// </para><para>
        /// Scenario 2 - SelectPeer:
        /// PeerAddressManager contains 2 peers.
        /// 1 Peer that is within self endpoint tracker.
        /// 1 Peer not withing self endpoint tracker.
        /// </para><para>
        /// We ask for a peer.
        /// </para><para>
        /// Result:
        /// Peer returned is not the one within self endpoint tracker.
        /// </para>
        /// </summary>
        [Fact]
        public void SelectPeersForDiscovery_WhenPeerAddressesContainsOwnIPEndoint_DoesNotReturnOwnEndpoint()
        {
            var selfIpEndPoint = new IPEndPoint(new IPAddress(1), 123);
            var selfPeerAddress = new PeerAddress() { Endpoint = selfIpEndPoint };
            selfPeerAddress.SetDiscoveredFrom(DateTime.MinValue);

            var otherIpEndPoint = new IPEndPoint(new IPAddress(2), 345);
            var otherPeerAddress = new PeerAddress() { Endpoint = otherIpEndPoint };
            otherPeerAddress.SetDiscoveredFrom(DateTime.MinValue);

            var peerAddresses = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            peerAddresses.AddOrUpdate(selfIpEndPoint, selfPeerAddress, (x, y) => selfPeerAddress);
            peerAddresses.AddOrUpdate(otherIpEndPoint, otherPeerAddress, (x, y) => otherPeerAddress);

            var selfEndpointTracker = new SelfEndpointTracker(this.extendedLoggerFactory);
            selfEndpointTracker.Add(selfIpEndPoint);

            var peerSelector = new PeerSelector(new DateTimeProvider(), this.LoggerFactory.Object, peerAddresses, selfEndpointTracker);

            IEnumerable<PeerAddress> peers = peerSelector.SelectPeersForDiscovery(2);

            Assert.Equal(otherPeerAddress, peers.Single());

            // Note: This for loop is because Random is currently a hard dependency rather than using dependency inversion.
            // It is not 100% safe without mocking random, so a workaround of 20 attempts used for now.
            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(otherPeerAddress, peerSelector.SelectPeer());
            }
        }

        [Fact]
        public void PeerSelector_ReturnConnectedPeers_AfterHandshakeFailure_WithAttemptsRemaining()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(new IPEndPoint(ipAddress, 80), IPAddress.Loopback);

            peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            DateTime handshakeAttempt = DateTime.UtcNow.AddSeconds(-80);

            PeerAddress peer = peerAddressManager.Peers.First();

            // Peer selected after one handshake failure.
            peer.SetHandshakeAttempted(handshakeAttempt);
            Assert.Equal(1, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer selected after two handshake failures.
            peer.SetHandshakeAttempted(handshakeAttempt);
            Assert.Equal(2, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer not selected after three handshake failures.
            peer.SetHandshakeAttempted(handshakeAttempt);
            Assert.Equal(3, peer.HandshakedAttempts);
            Assert.DoesNotContain(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));
        }

        [Fact]
        public void PeerSelector_ReturnConnectedPeers_AfterHandshakeFailure_ThresholdExceeded()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(new IPEndPoint(ipAddress, 80), IPAddress.Loopback);

            peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            DateTime handshakeAttempt = DateTime.UtcNow.AddSeconds(-80);

            PeerAddress peer = peerAddressManager.Peers.First();

            // Peer selected after one handshake failure.
            peer.SetHandshakeAttempted(handshakeAttempt.AddHours(-(PeerAddress.AttempThresholdHours + 4)));
            Assert.Equal(1, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer selected after two handshake failures.
            peer.SetHandshakeAttempted(handshakeAttempt.AddHours(-(PeerAddress.AttempThresholdHours + 3)));
            Assert.Equal(2, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer selected after two handshake failures when threshold time has elapsed.
            peer.SetHandshakeAttempted(handshakeAttempt.AddHours(-(PeerAddress.AttempThresholdHours + 2)));
            Assert.Equal(3, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));
        }

        [Fact]
        public void PeerSelector_ReturnConnectedPeers_AfterHandshakeFailure_HandshakeSucceeded_ResetsCounters()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(new IPEndPoint(ipAddress, 80), IPAddress.Loopback);

            peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            DateTime handshakeAttempt = DateTime.UtcNow.AddSeconds(-80);

            PeerAddress peer = peerAddressManager.Peers.First();

            // Peer selected after one handshake failure.
            peer.SetHandshakeAttempted(handshakeAttempt);
            Assert.Equal(1, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer selected after two handshake failures.
            peer.SetHandshakeAttempted(handshakeAttempt);
            Assert.Equal(2, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer attempt counter and last attempt reset after successful handshake.
            peer.SetHandshaked(handshakeAttempt);
            Assert.Equal(0, peer.HandshakedAttempts);
            Assert.Null(peer.LastHandshakeAttempt);
        }

        [Fact]
        public void PeerSelector_ReturnConnectedPeers_AfterThresholdExpired_ResetCounters()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            DataFolder peerFolder = CreateDataFolder(this);
            PeerAddressManager peerAddressManager = this.CreatePeerAddressManager(peerFolder);
            peerAddressManager.AddPeer(new IPEndPoint(ipAddress, 80), IPAddress.Loopback);

            peerAddressManager.PeerConnected(new IPEndPoint(ipAddress, 80), DateTime.UtcNow.AddSeconds(-80));
            DateTime firstHandshakeAttemptTime = DateTime.UtcNow.AddSeconds(-80);

            PeerAddress peer = peerAddressManager.Peers.First();

            // Peer selected after one handshake failure.
            peer.SetHandshakeAttempted(firstHandshakeAttemptTime);
            Assert.Equal(1, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer selected after two handshake failures.
            peer.SetHandshakeAttempted(firstHandshakeAttemptTime);
            Assert.Equal(2, peer.HandshakedAttempts);
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));

            // Peer attempt counter and last attempt reset when threshold time has elapsed.
            peer.SetHandshakeAttempted(firstHandshakeAttemptTime.AddHours(-(PeerAddress.AttempThresholdHours + 2)));
            Assert.Contains(peer, peerAddressManager.PeerSelector.FilterBadHandshakedPeers(peerAddressManager.Peers));
            Assert.Equal(0, peer.HandshakedAttempts);
        }

        [Fact]
        public void PeerSelector_HasAllPeersReachedConnectionThreshold()
        {
            var peerAddress = new PeerAddress() { Endpoint = new IPEndPoint(new IPAddress(2), 345) };

            var peerAddresses = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            peerAddresses.AddOrUpdate(peerAddress.Endpoint, peerAddress, (x, y) => peerAddress);

            var peerSelector = new PeerSelector(new DateTimeProvider(), this.LoggerFactory.Object, peerAddresses, new SelfEndpointTracker(this.extendedLoggerFactory));

            Assert.False(peerSelector.HasAllPeersReachedConnectionThreshold());

            for (int i = 0; i < 5; i++)
                peerAddress.SetAttempted(DateTime.UtcNow);

            Assert.True(peerSelector.HasAllPeersReachedConnectionThreshold());
        }

        [Fact]
        public void PeerSelector_CanResetAttempts()
        {
            var peerAddress = new PeerAddress() { Endpoint = new IPEndPoint(new IPAddress(2), 345) };

            var bannedPeerAddress = new PeerAddress() { Endpoint = new IPEndPoint(new IPAddress(3), 346) };
            bannedPeerAddress.BanUntil = DateTime.UtcNow.AddHours(1);

            var peerAddresses = new ConcurrentDictionary<IPEndPoint, PeerAddress>();
            peerAddresses.AddOrUpdate(peerAddress.Endpoint, peerAddress, (x, y) => peerAddress);
            peerAddresses.AddOrUpdate(bannedPeerAddress.Endpoint, bannedPeerAddress, (x, y) => bannedPeerAddress);

            var peerSelector = new PeerSelector(new DateTimeProvider(), this.LoggerFactory.Object, peerAddresses, new SelfEndpointTracker(this.extendedLoggerFactory));

            for (int i = 0; i < 5; i++)
                peerAddress.SetAttempted(DateTime.UtcNow);

            Assert.Equal(5, peerAddress.ConnectionAttempts);
            Assert.True(peerSelector.HasAllPeersReachedConnectionThreshold());

            peerSelector.ResetConnectionAttemptsOnNotBannedPeers();

            Assert.Equal(0, peerAddress.ConnectionAttempts);
        }

        private PeerAddressManager CreatePeerAddressManager(DataFolder peerFolder)
        {
            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory,
                new SelfEndpointTracker(this.extendedLoggerFactory));
            return peerAddressManager;
        }
    }
}