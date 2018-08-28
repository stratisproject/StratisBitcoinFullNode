using System.Net;
using System.Threading;
using Moq;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common.Logging;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.P2P
{
    public sealed class PeerAddressManagerBehaviourTests : LogsTestBase
    {
        private readonly ExtendedLoggerFactory extendedLoggerFactory;
        private readonly INetworkPeerFactory networkPeerFactory;

        public PeerAddressManagerBehaviourTests()
        {
            this.extendedLoggerFactory = new ExtendedLoggerFactory();
            this.extendedLoggerFactory.AddConsoleWithFilters();

            this.networkPeerFactory = new NetworkPeerFactory(this.Network, 
                DateTimeProvider.Default, 
                this.extendedLoggerFactory, 
                new PayloadProvider().DiscoverPayloads(), 
                new SelfEndpointTracker(this.extendedLoggerFactory),
                new Mock<IInitialBlockDownloadState>().Object,
                new Configuration.Settings.ConnectionManagerSettings());
        }

        [Fact]
        public void PeerAddressManagerBehaviour_ReceivedPing_UpdateLastSeen()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.extendedLoggerFactory));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.HandShaked);

            var messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            networkPeer.SetupGet(n => n.MessageReceived).Returns(messageReceived);

            var stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            networkPeer.SetupGet(n => n.StateChanged).Returns(stateChanged); 

            var behaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, addressManager, this.extendedLoggerFactory) { Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover };
            behaviour.Attach(networkPeer.Object);

            var incomingMessage = new IncomingMessage();
            incomingMessage.Message = new Message(new PayloadProvider().DiscoverPayloads())
            {
                Magic = this.Network.Magic,
                Payload = new PingPayload(),
            };

            //Trigger the event handler
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();

            PeerAddress peer = addressManager.FindPeer(endpoint);
            Assert.Equal(DateTimeProvider.Default.GetUtcNow().Date, peer.LastSeen.Value.Date);
        }

        [Fact]
        public void PeerAddressManagerBehaviour_ReceivedPong_UpdateLastSeen()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.extendedLoggerFactory));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.HandShaked);

            var messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            networkPeer.SetupGet(n => n.MessageReceived).Returns(messageReceived);

            var stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            networkPeer.SetupGet(n => n.StateChanged).Returns(stateChanged);

            var behaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, addressManager, this.extendedLoggerFactory) { Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover };
            behaviour.Attach(networkPeer.Object);

            var incomingMessage = new IncomingMessage();
            incomingMessage.Message = new Message(new PayloadProvider().DiscoverPayloads())
            {
                Magic = this.Network.Magic,
                Payload = new PingPayload(),
            };

            //Trigger the event handler
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();

            PeerAddress peer = addressManager.FindPeer(endpoint);
            Assert.Equal(DateTimeProvider.Default.GetUtcNow().Date, peer.LastSeen.Value.Date);
        }

        [Fact]
        public void PeerAddressManagerBehaviour_DoesntSendAddress_Outbound()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.extendedLoggerFactory));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.HandShaked);
            networkPeer.SetupGet(n => n.Inbound).Returns(false); // Outbound

            var messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            networkPeer.SetupGet(n => n.MessageReceived).Returns(messageReceived);

            var stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            networkPeer.SetupGet(n => n.StateChanged).Returns(stateChanged);

            var behaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, addressManager, this.extendedLoggerFactory) { Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover };
            behaviour.Attach(networkPeer.Object);

            var incomingMessage = new IncomingMessage();
            incomingMessage.Message = new Message(new PayloadProvider().DiscoverPayloads())
            {
                Magic = this.Network.Magic,
                Payload = new GetAddrPayload(),
            };

            // Event handler triggered, but SendMessage shouldn't be called as the node is Outbound.
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();
            networkPeer.Verify(x => x.SendMessageAsync(It.IsAny<Payload>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public void PeerAddressManagerBehaviour_DoesntSendAddress_Twice()
        {
            IPAddress ipAddress = IPAddress.Parse("::ffff:192.168.0.1");
            var endpoint = new IPEndPoint(ipAddress, 80);

            DataFolder peerFolder = CreateDataFolder(this);
            var addressManager = new PeerAddressManager(DateTimeProvider.Default, peerFolder, this.LoggerFactory.Object, new SelfEndpointTracker(this.extendedLoggerFactory));
            addressManager.AddPeer(endpoint, IPAddress.Loopback);

            var networkPeer = new Mock<INetworkPeer>();
            networkPeer.SetupGet(n => n.PeerEndPoint).Returns(endpoint);
            networkPeer.SetupGet(n => n.State).Returns(NetworkPeerState.HandShaked);
            networkPeer.SetupGet(n => n.Inbound).Returns(true);

            var messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();
            networkPeer.SetupGet(n => n.MessageReceived).Returns(messageReceived);

            var stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            networkPeer.SetupGet(n => n.StateChanged).Returns(stateChanged);

            var behaviour = new PeerAddressManagerBehaviour(DateTimeProvider.Default, addressManager, this.extendedLoggerFactory) { Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover };
            behaviour.Attach(networkPeer.Object);

            var incomingMessage = new IncomingMessage();
            incomingMessage.Message = new Message(new PayloadProvider().DiscoverPayloads())
            {
                Magic = this.Network.Magic,
                Payload = new GetAddrPayload(),
            };

            // Event handler triggered several times 
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();
            networkPeer.Object.MessageReceived.ExecuteCallbacksAsync(networkPeer.Object, incomingMessage).GetAwaiter().GetResult();

            // SendMessage should only be called once.
            networkPeer.Verify(x => x.SendMessageAsync(It.IsAny<Payload>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
