using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
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
            var endPoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            addressManager.AddPeer(endPoint, IPAddress.Loopback);

            addressManager.PeerConnected(endPoint, DateTime.UtcNow);

            addressManager.PeerAttempted(endPoint, DateTime.UtcNow);
            addressManager.PeerAttempted(endPoint, DateTime.UtcNow);
            addressManager.PeerAttempted(endPoint, DateTime.UtcNow);

            addressManager.PeerConnected(endPoint, DateTime.UtcNow);

            var peerOne = addressManager.FindPeer(endPoint);

            Assert.Equal(0, peerOne.ConnectionAttempts);
            Assert.Null(peerOne.LastAttempt);
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
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            var peers = peerAddressManager.PeerSelector.Fresh();
            Assert.Equal(3, peers.Count());
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set.
        ///
        /// Scenario:
        /// Peer 1 has had a connection attempt (in the last 60 seconds).
        /// Peer 2 has had a connection attempt (more than 1 hour ago).
        /// Peer 3 has had a connection attempt (more than 1 hour ago).
        ///
        /// Result:
        /// Peers 2 and 3 are in the attempted set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_Scenario1()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerAttempted(endPointOne, DateTime.UtcNow);
            peerAddressManager.PeerAttempted(endPointTwo, DateTime.UtcNow.AddHours(-2));
            peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow.AddHours(-2));

            var peers = peerAddressManager.PeerSelector.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.EndPoint.Match(endPointOne));
        }

        /// <summary>
        /// Ensures that a particular peer is returned from the attempted peers
        /// set.
        ///
        /// Scenario:
        /// Peer 1 has had a connection attempt (more than 1 hour ago).
        /// Peer 2 has had a connection attempt (more than 1 hour ago).
        /// Peer 3 was attempted unsuccessfully more than 10 times.
        ///
        /// Result:
        /// Peers 1 and 2 are in the attempted set.
        /// </summary>
        [Fact]
        public void PeerState_TestReturnFromPeerAttemptedSet_Scenario2()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerAttempted(endPointOne, DateTime.UtcNow.AddHours(-2));
            peerAddressManager.PeerAttempted(endPointTwo, DateTime.UtcNow.AddHours(-820));

            for (int i = 0; i < 15; i++)
            {
                peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow);
            }

            var peers = peerAddressManager.PeerSelector.Attempted();
            Assert.Equal(2, peers.Count());
            Assert.DoesNotContain(peers, p => p.EndPoint.Match(endPointThree));
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
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerConnected(endPointOne, DateTime.UtcNow);
            peerAddressManager.PeerConnected(endPointTwo, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow);

            var peers = peerAddressManager.PeerSelector.Connected();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.EndPoint.Match(endPointTwo));
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
            var endPointOne = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var endPointTwo = new IPEndPoint(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var endPointThree = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeer(endPointOne, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointTwo, IPAddress.Loopback);
            peerAddressManager.AddPeer(endPointThree, IPAddress.Loopback);

            peerAddressManager.PeerConnected(endPointOne, DateTime.UtcNow);
            peerAddressManager.PeerHandshaked(endPointOne, DateTime.UtcNow);

            peerAddressManager.PeerConnected(endPointTwo, DateTime.UtcNow.AddSeconds(-80));
            peerAddressManager.PeerHandshaked(endPointTwo, DateTime.UtcNow.AddSeconds(-80));

            peerAddressManager.PeerAttempted(endPointThree, DateTime.UtcNow);

            var peers = peerAddressManager.PeerSelector.Handshaked();
            Assert.Single(peers);
            Assert.Contains(peers, p => p.EndPoint.Match(endPointTwo));
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
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
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
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
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
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
            peerAddressManager.AddPeers(peersToAdd.ToArray(), IPAddress.Loopback);

            for (int i = 1; i <= 7; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peerAddressManager.PeerAttempted(new NetworkAddress(ipAddress, 80).Endpoint, DateTime.UtcNow.AddHours(-2));
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
            var peersToAdd = new List<IPEndPoint>();

            for (int i = 1; i <= 15; i++)
            {
                var ipAddress = IPAddress.Parse(string.Format("::ffff:192.168.0.{0}", i));
                peersToAdd.Add(new IPEndPoint(ipAddress, 80));
            }

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var peerAddressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.extendedLoggerFactory);
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
    }
}