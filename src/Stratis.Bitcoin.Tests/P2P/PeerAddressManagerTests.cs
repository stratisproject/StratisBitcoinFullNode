using System;
using System.IO;
using System.Linq;
using System.Net;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerAddressManagerTests : TestBase
    {
        [Fact]
        public void CanSaveAndLoadPeerAddressFile_PeerConnected()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            addressManager.PeerAttempted(networkAddress.Endpoint, DateTimeOffset.Now);
            addressManager.PeerConnected(networkAddress.Endpoint, DateTimeOffset.Now);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(networkAddress.Endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.NetworkAddress.Endpoint.Address.ToString());
            Assert.Equal(DateTime.Today.Date, savedPeer.NetworkAddress.Time.Date);
            Assert.Equal(80, savedPeer.NetworkAddress.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(DateTime.Today.Date, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Null(savedPeer.LastConnectionHandshake);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void CanSaveAndLoadPeerAddressFile_PeerHandshaked()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));

            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            addressManager.PeerAttempted(networkAddress.Endpoint, DateTimeOffset.Now);
            addressManager.PeerConnected(networkAddress.Endpoint, DateTimeOffset.Now);
            addressManager.PeerHandshaked(networkAddress.Endpoint, DateTimeOffset.Now);

            addressManager.SavePeers();
            addressManager.LoadPeers();

            var savedPeer = addressManager.FindPeer(networkAddress.Endpoint);

            Assert.Equal("::ffff:192.168.0.1", savedPeer.NetworkAddress.Endpoint.Address.ToString());
            Assert.Equal(DateTime.Today.Date, savedPeer.NetworkAddress.Time.Date);
            Assert.Equal(80, savedPeer.NetworkAddress.Endpoint.Port);
            Assert.Equal(0, savedPeer.ConnectionAttempts);
            Assert.Equal(DateTime.Today.Date, savedPeer.LastConnectionSuccess.Value.Date);
            Assert.Equal(DateTime.Today.Date, savedPeer.LastConnectionHandshake.Value.Date);
            Assert.Equal("127.0.0.1", savedPeer.Loopback.ToString());
        }

        [Fact]
        public void PeerConnected_AllConnectionDataGetsReset()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var networkAddress = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(networkAddress, IPAddress.Loopback);

            addressManager.PeerConnected(networkAddress.Endpoint, DateTimeOffset.Now);

            addressManager.PeerAttempted(networkAddress.Endpoint, DateTimeOffset.Now);
            addressManager.PeerAttempted(networkAddress.Endpoint, DateTimeOffset.Now);
            addressManager.PeerAttempted(networkAddress.Endpoint, DateTimeOffset.Now);

            addressManager.PeerConnected(networkAddress.Endpoint, DateTimeOffset.Now);

            var peerOne = addressManager.FindPeer(networkAddress.Endpoint);

            Assert.Equal(0, peerOne.ConnectionAttempts);
            Assert.Null(peerOne.LastConnectionAttempt);
            Assert.NotNull(peerOne.LastConnectionSuccess);
        }

        /// <summary>
        /// Ensures that after a peer has had a connection attempt,
        /// that it doesn't get selected to be connected to again.
        /// </summary>
        [Fact]
        public void CanSelectRandomPeerToConnectTo_AllPeersAreNew()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.4");
            var addressFour = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);
            addressManager.AddPeer(addressFour, IPAddress.Loopback);

            var randomPeer = addressManager.SelectPeerToConnectTo();
            addressManager.PeerAttempted(randomPeer.Endpoint, DateTimeOffset.Now);

            var selected = addressManager.Peers.New().FirstOrDefault(p => p.NetworkAddress.Endpoint.Match(randomPeer.Endpoint));
            Assert.Null(selected);
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred.
        ///
        /// Scenario 1:
        /// No peers in the database has had a connection attempted to or previously connected to.
        ///
        /// Result:
        /// All 3 peers can be connected to.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_NeverBeenConnectedTo_Scenario1()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(3, networkAddresses.Count());
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred.
        ///
        /// Scenario 2:
        /// Peer 2 has had a connection attempted to.
        ///
        /// Result:
        /// All 3 peers can be connected to.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_NeverBeenConnectedTo_Scenario2()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerAttempted(addressTwo.Endpoint, DateTimeOffset.Now);

            var peers = addressManager.SelectPeersToConnectTo();
            Assert.Equal(3, peers.Count());
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred.
        ///
        /// Scenario 3:
        /// Peer 2 has had 2 connection attempts.
        /// Peer 2 has last attempted more than 60 secs ago.
        ///
        /// Result:
        /// All 3 peers can be connected to.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_NeverBeenConnectedTo_Scenario3()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerAttempted(addressTwo.Endpoint, DateTimeOffset.Now);
            addressManager.PeerAttempted(addressTwo.Endpoint, DateTimeOffset.Now - TimeSpan.FromSeconds(70));

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(2, networkAddresses.Count());

            Assert.Null(networkAddresses.FirstOrDefault(n => n.Endpoint.Address.ToString() == addressTwo.Endpoint.Address.ToString()));
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred.
        ///
        /// Scenario 3:
        /// Peer 1 has had 2 connection attempts.
        /// Peer 1 has attempted within the last 60 secs.
        ///
        /// Result:
        /// Peer 1 is filtered out.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_NeverBeenConnectedTo_Scenario4()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerAttempted(addressOne.Endpoint, DateTimeOffset.Now);
            addressManager.PeerAttempted(addressOne.Endpoint, DateTimeOffset.Now.AddSeconds(-65));

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(2, networkAddresses.Count());

            Assert.Null(networkAddresses.FirstOrDefault(n => n.Endpoint.Address.ToString() == addressOne.Endpoint.Address.ToString()));
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred.
        ///
        /// Scenario 4:
        /// Peer 2 has had more than the maximum amount of connection attempts.
        ///
        /// Result:
        /// There are 2 peers to connect to.
        /// Peer 1 has been filtered out.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_NeverBeenConnectedTo_Scenario5()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerAttempted(addressOne.Endpoint, DateTimeOffset.Now);
            addressManager.PeerAttempted(addressOne.Endpoint, DateTimeOffset.Now);
            addressManager.PeerAttempted(addressOne.Endpoint, DateTimeOffset.Now);

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(2, networkAddresses.Count());

            Assert.Null(networkAddresses.FirstOrDefault(n => n.Endpoint.Address.ToString() == addressOne.Endpoint.Address.ToString()));
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred after it has been
        /// connected to before.
        ///
        /// Scenario 1:
        /// Peer 3 has had a successful connection made to it.
        ///
        /// Result:
        /// All 3 peers can be connected to.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_HasBeenConnectedTo_Scenario1()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerConnected(addressThree.Endpoint, DateTimeOffset.Now);

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(3, networkAddresses.Count());
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred after it has been
        /// connected to before.
        ///
        /// Scenario 2:
        /// Peer 3 has had a successful connection made to it but it
        /// last happened 8 days ago.
        ///
        /// Result:
        /// There are 2 peers to connect to.
        /// Peer 3 has been filtered out.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_HasBeenConnectedTo_Scenario2()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerConnected(addressThree.Endpoint, DateTimeOffset.Now.AddDays(-8));

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(2, networkAddresses.Count());

            Assert.Null(networkAddresses.FirstOrDefault(n => n.Endpoint.Address.ToString() == addressThree.Endpoint.Address.ToString()));
        }

        /// <summary>
        /// Ensures that a particular peer can be regarded as preferred after it has been
        /// connected to before.
        ///
        /// Scenario 3:
        /// Peer 1 has had a successful connection made to.
        /// Peer 1 has had 11 unsuccessful attempts since then.
        ///
        /// Result:
        /// There are 2 peers to connect to.
        /// Peer 1 has been filtered out.
        /// </summary>
        [Fact]
        public void PeerCanBeReturnedAsPreferred_HasBeenConnectedTo_Scenario3()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.2");
            var addressTwo = new NetworkAddress(ipAddress, 80);

            ipAddress = IPAddress.Parse("::ffff:192.168.0.3");
            var addressThree = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);
            addressManager.AddPeer(addressTwo, IPAddress.Loopback);
            addressManager.AddPeer(addressThree, IPAddress.Loopback);

            addressManager.PeerConnected(addressOne.Endpoint, DateTimeOffset.Now.AddDays(-5));
            for (int i = 0; i < 11; i++)
            {
                addressManager.PeerAttempted(addressOne.Endpoint, DateTimeOffset.Now);
            }

            var networkAddresses = addressManager.SelectPeersToConnectTo();
            Assert.Equal(2, networkAddresses.Count());

            Assert.Null(networkAddresses.FirstOrDefault(n => n.Endpoint.Address.ToString() == addressOne.Endpoint.Address.ToString()));
        }

        [Fact]
        public void PeerSelectability_Decreases_AfterEachFailedAttempt()
        {
            var ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var addressOne = new NetworkAddress(ipAddress, 80);

            var peerFolder = AssureEmptyDirAsDataFolder(Path.Combine(AppContext.BaseDirectory, "PeerAddressManager"));
            var addressManager = new PeerAddressManager(peerFolder);
            addressManager.AddPeer(addressOne, IPAddress.Loopback);

            var peer = addressManager.FindPeer(addressOne.Endpoint);
            peer.Attempted(DateTimeOffset.Now);
            var resultOne = peer.Selectability;

            peer.Attempted(DateTimeOffset.Now);
            var resultTwo = peer.Selectability;

            Assert.True(resultOne > resultTwo);
        }
    }
}
