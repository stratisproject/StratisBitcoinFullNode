﻿using System;
using System.IO;
using System.Net;
using NBitcoin.Protocol;
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
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(networkAddress.Endpoint, applicableDate);
            addressManager.PeerConnected(networkAddress.Endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(networkAddress.Endpoint);

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
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(networkAddress.Endpoint, applicableDate);
            addressManager.PeerConnected(networkAddress.Endpoint, applicableDate);
            addressManager.PeerHandshaked(networkAddress.Endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(networkAddress.Endpoint);

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
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(peerFolder, this.loggerFactory);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var applicableDate = DateTime.UtcNow.Date;

            addressManager.PeerAttempted(networkAddress.Endpoint, applicableDate);
            addressManager.PeerConnected(networkAddress.Endpoint, applicableDate);
            addressManager.PeerHandshaked(networkAddress.Endpoint, applicableDate);
            addressManager.PeerSeen(networkAddress.Endpoint, applicableDate);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(networkAddress.Endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.EndPoint.Address.ToString());
            Assert.Equal(80, savedPeer.EndPoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(applicableDate, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal(applicableDate, savedPeer.LastSeen.Value.Date);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerAddressManager_AttemptThresholdReach_ResetAttempts()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.loggerFactory);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            var applicableDate = DateTimeProvider.Default.GetUtcNow();

            for (int i = 0; i < 10; i++)
            {
                addressManager.PeerAttempted(networkAddress.Endpoint, applicableDate.AddHours(-i));
            }

            addressManager.PeerAttempted(networkAddress.Endpoint, applicableDate);

            var savedPeer = addressManager.FindPeer(networkAddress.Endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.EndPoint.Address.ToString());
            Assert.Equal(80, savedPeer.EndPoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Null(savedPeer.LastConnectionSuccess);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }
    }
}