using System;
using System.Net;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerAddressManagerTests : LogsTestBase
    {
        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerConnected()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
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
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerHandshaked()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
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
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerSeen()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
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
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdReached_ResetAttempts()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 10 failed attempts
            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            //Ensure that the last attempt was more than 12 hours ago
            addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-13));

            //This call should now reset the counts
            DateTime resetTimestamp = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endpoint, resetTimestamp);

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal(1, savedPeer.ConnectionAttempts);
            Assert.Equal(resetTimestamp, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdTimeNotReached_DoNotReset()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker());
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            DateTime applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 10 failed attempts
            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(endpoint, applicableDate.AddHours(-i));
            }

            //Capture the last attempt timestamp
            DateTime lastAttempt = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endpoint, lastAttempt);

            PeerAddress savedPeer = addressManager.FindPeer(endpoint);

            Assert.Equal(11, savedPeer.ConnectionAttempts);
            Assert.Equal(lastAttempt, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }
    }
}
