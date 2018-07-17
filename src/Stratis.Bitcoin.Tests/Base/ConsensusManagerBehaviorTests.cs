using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ConsensusManagerBehaviorTests
    {
        private bool IsIBD = false;

        private readonly ExtendedLoggerFactory loggerFactory;

        public ConsensusManagerBehaviorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            Mock<INetworkPeer> peer = this.CreatePeerMock();

            ConsensusManagerBehavior behavior = this.CreateBehavior();
            behavior.Attach(peer.Object);
            peer.Setup(x => x.Behavior<ConsensusManagerBehavior>()).Returns(() => behavior);
        }

        private Mock<INetworkPeer> CreatePeerMock()
        {
            var peer = new Mock<INetworkPeer>();

            var connection = new NetworkPeerConnection(Network.StratisMain, peer.Object, new TcpClient(), 0, (message, token) => Task.CompletedTask,
                new DateTimeProvider(), this.loggerFactory, new PayloadProvider());

            peer.SetupGet(networkPeer => networkPeer.Connection).Returns(connection);

            var connectionParameters = new NetworkPeerConnectionParameters();
            VersionPayload version = connectionParameters.CreateVersion(new IPEndPoint(1, 1), Network.StratisMain, new DateTimeProvider().GetTimeOffset());
            version.Services = NetworkPeerServices.Network;

            peer.SetupGet(x => x.PeerVersion).Returns(version);
            peer.SetupGet(x => x.State).Returns(NetworkPeerState.HandShaked);
            peer.SetupGet(x => x.MessageReceived).Returns(new AsyncExecutionEvent<INetworkPeer, IncomingMessage>());

            return peer;
        }

        private ConsensusManagerBehavior CreateBehavior()
        {
            var chain = new ConcurrentChain(Network.StratisMain);
            List<ChainedHeader> headers = ChainedHeadersHelper.CreateConsequtiveHeaders(10);
            chain.SetTip(headers.Last());

            var ibdState = new Mock<IInitialBlockDownloadState>();
            ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => this.IsIBD);

            ConsensusManager cm = new Mock<ConsensusManager>().Object;

            var behavior = new ConsensusManagerBehavior(chain, ibdState.Object, cm, new Mock<IPeerBanning>().Object,
                new Mock<IConnectionManager>().Object, this.loggerFactory);

            return behavior;
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10).
        /// Cached headers contain nothing. <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure that ResyncAsync wasn't called on the peer, CM.HeadersPresented wasn't called. Return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CTAdvancedBuNoCachedHeaders()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 11 to 12.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> == 12, Cached headers are empty and ResyncAsync was called.
        /// Make sure return headers up to header 12 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedFully()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10).
        /// Cached headers have items 11 to 50.  Setup CM.HeadersPresented to stop consumption when block 40 is reached.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure ExpectedPeerTip == 40,
        /// cached headers contain 10 items (41 to 50) and ResyncAsync wasn't called.
        /// Make sure return headers up to header 40 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedPartially()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 14 to 15.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure that cached headers contain no elements,
        /// ResyncAsync is called and <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is still 10. Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_NotAbleToConnectCachedHeaders()
        {

        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 11 to 12.
        /// Peer is not attached (AttachedPeer == null). <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_PeerNotAttached()
        {

        }
    }
}
