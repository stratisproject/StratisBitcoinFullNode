using System;
using System.IO;
using System.Net;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerAddressManagerTests : TestBase
    {
        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerConnected()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(endPoint, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endPoint, applicableDate);
            addressManager.PeerConnected(endPoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(endPoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.EndPoint.Address.ToString());
            Assert.Equal(80, savedPeer.EndPoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerHandshaked()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(endPoint, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endPoint, applicableDate);
            addressManager.PeerConnected(endPoint, applicableDate);
            addressManager.PeerHandshaked(endPoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(endPoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.EndPoint.Address.ToString());
            Assert.Equal(80, savedPeer.EndPoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerFile_CanSaveAndLoadPeers_PeerSeen()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(endPoint, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(endPoint, applicableDate);
            addressManager.PeerConnected(endPoint, applicableDate);
            addressManager.PeerHandshaked(endPoint, applicableDate);
            addressManager.PeerSeen(endPoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(endPoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.EndPoint.Address.ToString());
            Assert.Equal(80, savedPeer.EndPoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastSeen.Value.Date);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdReached_ResetAttempts()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(endPoint, IPAddress.Loopback);

            var applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 10 failed attempts
            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(endPoint, applicableDate.AddHours(-i));
            }

            //Ensure that the last attempt was more than 12 hours ago
            addressManager.PeerAttempted(endPoint, applicableDate.AddHours(-13));

            //This call should now reset the counts
            var resetTimestamp = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endPoint, resetTimestamp);

            var savedPeer = addressManager.FindPeer(endPoint);

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
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endPoint = new IPEndPoint(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(endPoint, IPAddress.Loopback);

            var applicableDate = DateTimeProvider.Default.GetUtcNow();

            //Ensure that there was 10 failed attempts
            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(endPoint, applicableDate.AddHours(-i));
            }

            //Capture the last attempt timestamp
            var lastAttempt = DateTimeProvider.Default.GetUtcNow();
            addressManager.PeerAttempted(endPoint, lastAttempt);

            var savedPeer = addressManager.FindPeer(endPoint);

            Assert.Equal(11, savedPeer.ConnectionAttempts);
            Assert.Equal(lastAttempt, savedPeer.LastAttempt);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Null(savedPeer.LastSeen);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }
    }
}