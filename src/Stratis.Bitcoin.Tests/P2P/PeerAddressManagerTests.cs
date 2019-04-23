using System;
using System.Linq;
using System.Net;
using FluentAssertions;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerAddressManagerTests : LogsTestBase
    {
        private readonly ConnectionManagerSettings connectionManagerSettings;

        public PeerAddressManagerTests()
        {
            this.connectionManagerSettings = new ConnectionManagerSettings(NodeSettings.Default(new StratisRegTest()));
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerConnected()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object,
                new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endpoint, applicableDate);
            addressManager.PeerConnected(endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.Endpoint.Address.ToString());
            Assert.Equal(80, savedPeer.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Equal("::ffff:127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerHandshaked()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object,
                new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endpoint, applicableDate);
            addressManager.PeerConnected(endpoint, applicableDate);
            addressManager.PeerHandshaked(endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.Endpoint.Address.ToString());
            Assert.Equal(80, savedPeer.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal("::ffff:127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerSeen()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object,
                new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endpoint, applicableDate);
            addressManager.PeerConnected(endpoint, applicableDate);
            addressManager.PeerHandshaked(endpoint, applicableDate);
            addressManager.PeerSeen(endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.Endpoint.Address.ToString());
            Assert.Equal(80, savedPeer.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastSeen.Value.Date);
            Assert.Equal("::ffff:127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_ResetBannedPeers()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            var addedPeer = addressManager.AddPeer(endpoint, IPAddress.Loopback);

            addedPeer.BanReason = "test";
            addedPeer.BanScore = 0;
            addedPeer.BanTimeStamp = DateTime.UtcNow.AddHours(-2);
            addedPeer.BanUntil = DateTime.UtcNow.AddHours(-1);

            addressManager.SavePeers();

            addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.LoadPeers();

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Null(savedPeer.BanReason);
            Assert.Null(savedPeer.BanScore);
            Assert.Null(savedPeer.BanTimeStamp);
            Assert.Null(savedPeer.BanUntil);
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_ResetAttemptThresholdReachedPeers()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTimeProvider.Default.GetUtcNow();

            // Set the peer's failed attempts count to 4.
            for (int i = 0; i < 4; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            // Ensure that the last attempt (5) was more than 12 hours ago.
            addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-13));

            // Ensure that the peer is still returned from the selector.
            var peer = addressManager.PeerSelector.SelectPeer();
            Assert.Equal(peer.Endpoint, endpoint);

            // Persist the peers to the json file.
            addressManager.SavePeers();

            // Creat a new address manager instance and load the peers from file.
            addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.LoadPeers();

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            // The peer's attempt thresholds should now be reset.
            Assert.False(savedPeer.Attempted);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Null(savedPeer.LastAttempt);
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdReached_ResetAttempts()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTimeProvider.Default.GetUtcNow();

            // Set the peer's failed attempts count to 4.
            for (int i = 0; i < 4; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            // Ensure that the last attempt (5) was more than 12 hours ago.
            addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-13));

            // Ensure that the peer is still returned from the selector.
            var peer = addressManager.PeerSelector.SelectPeer();
            Assert.Equal(peer.Endpoint, endpoint);

            // This call should now reset the counts
            DateTime resetTimestamp = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endpoint, resetTimestamp);

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal(1, savedPeer.ConnectionAttempts);
            Assert.Equal(resetTimestamp, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("::ffff:127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdTimeNotReached_DoNotReset()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 4 failed attempts
            for (int i = 0; i < 4; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            //Capture the last attempt timestamp
            DateTime lastAttempt = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endpoint, lastAttempt);

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal(5, savedPeer.ConnectionAttempts);
            Assert.Equal(lastAttempt, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("::ffff:127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AddPeer_then_RemovePeer_works_with_IPV4_and_IPV6()
        {
            DataFolder peerFolder = CreateDataFolder(this);

            var source = new IPEndPoint(IPAddress.Parse("124.54.54.2"), 22);

            var ipV4Addresses = new[]
            {
                IPAddress.Parse("21.23.0.1"),
                IPAddress.Parse("143.12.0.1"),
                IPAddress.Parse("99.87.44.1"),
                IPAddress.Parse("192.168.0.1"),
            };
            var ipV4Endpoints = ipV4Addresses.Select((a, i) => new IPEndPoint(a, i)).ToArray();

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object,
                new SelfEndpointTracker(this.LoggerFactory.Object, this.connectionManagerSettings));
            addressManager.AddPeers(ipV4Endpoints, source.Address);
            addressManager.Peers.Select(a => a.Endpoint.Address.MapToIPv6())
                .Distinct().Count().Should().Be(4);

            var ipV6Addresses = new[]
            {
                IPAddress.Parse("1050:1:0:0:5:600:300c:326b"),
                IPAddress.Parse("23d1:1:34b5:0:5:600:300c:326b"),
                ipV4Addresses[3].MapToIPv6(),
            };
            var ipV6Endpoints = ipV6Addresses.Select((a, i) => new IPEndPoint(a, i)).ToArray();
            addressManager.AddPeers(ipV6Endpoints, source.Address);

            addressManager.Peers.Select(a => a.Endpoint.Address.MapToIPv6())
                .Distinct().Count().Should().Be(6);

            addressManager.RemovePeer(ipV4Endpoints[0]);
            addressManager.Peers.Select(a => a.Endpoint.Address.MapToIPv6())
                .Distinct().Count().Should().Be(5);

            addressManager.RemovePeer(ipV6Endpoints[1]);
            addressManager.Peers.Select(a => a.Endpoint.Address.MapToIPv6())
                .Distinct().Count().Should().Be(4);
        }
    }
}
