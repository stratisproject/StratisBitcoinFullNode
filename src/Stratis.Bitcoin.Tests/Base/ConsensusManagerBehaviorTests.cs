using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

        private AsyncExecutionEvent<INetworkPeer, NetworkPeerState> stateChanged;
        private AsyncExecutionEvent<INetworkPeer, IncomingMessage> messageReceived;

        private Mock<INetworkPeer> peerMock;

        private readonly List<ChainedHeader> headers;

        private readonly ExtendedLoggerFactory loggerFactory;

        private int getHeadersPayloadSentTimes;

        /// <summary>How many times behavior called the <see cref="ConsensusManager.HeadersPresented"/>.</summary>
        private int headersPresentedCalledTimes;

        public ConsensusManagerBehaviorTests()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();

            this.headers = ChainedHeadersHelper.CreateConsecutiveHeaders(100);
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

            this.stateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            this.messageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();

            peer.Setup(x => x.StateChanged).Returns(() => this.stateChanged);
            peer.Setup(x => x.MessageReceived).Returns(() => this.messageReceived);

            return peer;
        }

        private ConsensusManagerBehavior CreateAndAttachBehavior(ChainedHeader consensusTip,
            List<BlockHeader> cache = null, ChainedHeader expectedPeerTip = null, NetworkPeerState peerState = NetworkPeerState.HandShaked,
            Func<List<BlockHeader>, bool, ConnectNewHeadersResult> connectNewHeadersMethod = null)
        {
            // Chain
            var chain = new ConcurrentChain(Network.StratisMain);
            chain.SetTip(consensusTip);

            // Ibd
            var ibdState = new Mock<IInitialBlockDownloadState>();
            ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => this.IsIBD);

            // Consensus manager
            var cmMock = new Mock<IConsensusManager>();

            cmMock.Setup(x => x.HeadersPresented(It.IsAny<INetworkPeer>(), It.IsAny<List<BlockHeader>>(), It.IsAny<bool>()))
                .Returns((INetworkPeer p, List<BlockHeader> presentedHeaders, bool triggerDownload) =>
            {
                this.headersPresentedCalledTimes++;

                return connectNewHeadersMethod?.Invoke(presentedHeaders, triggerDownload);
            });

            cmMock.Setup(x => x.Tip).Returns(consensusTip);

            var cmBehavior = new ConsensusManagerBehavior(chain, ibdState.Object, cmMock.Object, new Mock<IPeerBanning>().Object,
                new Mock<IConnectionManager>().Object, this.loggerFactory);

            // Peer and behavior
            this.peerMock = this.CreatePeerMock();

            cmBehavior.Attach(this.peerMock.Object);
            this.peerMock.Setup(x => x.Behavior<ConsensusManagerBehavior>()).Returns(() => cmBehavior);
            this.peerMock.Setup(x => x.State).Returns(peerState);

            if (expectedPeerTip != null)
                cmBehavior.SetPrivatePropertyValue("ExpectedPeerTip", expectedPeerTip);

            if (cache != null)
                cmBehavior.SetPrivateVariableValue("cachedHeaders", cache);

            this.getHeadersPayloadSentTimes = 0;

            this.peerMock.Setup(x => x.SendMessageAsync(It.IsAny<Payload>(), It.IsAny<CancellationToken>())).Returns((Payload payload, CancellationToken token) =>
            {
                if (payload is GetHeadersPayload)
                    this.getHeadersPayloadSentTimes++;

                return Task.CompletedTask;
            });

            return cmBehavior;
        }

        private List<BlockHeader> getCachedHeaders(ConsensusManagerBehavior behavior)
        {
            return behavior.GetMemberValue("cachedHeaders") as List<BlockHeader>;
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10).
        /// Cached headers contain nothing. <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure that getHeaders payload wasn't sent to the peer, <see cref="ConsensusManager.HeadersPresented"/> wasn't called. Return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CTAdvancedBuNoCachedHeadersAsync()
        {
            ConsensusManagerBehavior behavior = this.CreateAndAttachBehavior(consensusTip: this.headers[5], cache: null, expectedPeerTip: this.headers[10]);

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Null(result);
            Assert.Equal(0, this.getHeadersPayloadSentTimes);
            Assert.Equal(0, this.headersPresentedCalledTimes);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 11 to 12.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> == 12, Cached headers are empty and getHeaders payload was sent to the peer.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedFullyAsync()
        {
            var cache = new List<BlockHeader>() {this.headers[11].Header, this.headers[12].Header};

            ConsensusManagerBehavior behavior = this.CreateAndAttachBehavior(consensusTip: this.headers[5], cache: cache,
                expectedPeerTip: this.headers[10], peerState: NetworkPeerState.HandShaked,
                connectNewHeadersMethod: (presentedHeaders, triggerDownload) =>
                {
                    if (presentedHeaders.Last() == this.headers[12].Header)
                    {
                        return new ConnectNewHeadersResult() {Consumed = this.headers[12] };
                    }

                    return null;
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[12], behavior.ExpectedPeerTip);
            Assert.Empty(this.getCachedHeaders(behavior));
            Assert.Equal(1, this.getHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[12]);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10).
        /// Cached headers have items 11 to 50.  Setup  <see cref="ConsensusManager.HeadersPresented"/> to stop consumption when block 40 is reached.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure ExpectedPeerTip == 40,
        /// cached headers contain 10 items (41 to 50) and getHeaders payload wasn't sent to the peer.
        /// Make sure in return value headers up to header 40 were consumed.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_CachedHeadersConsumedPartiallyAsync()
        {
            var cache = new List<BlockHeader>();
            for (int i= 11; i <= 50; i++)
                cache.Add(this.headers[i].Header);

            ConsensusManagerBehavior behavior = this.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    if (presentedHeaders.Last() == this.headers[50].Header)
                    {
                        return new ConnectNewHeadersResult() { Consumed = this.headers[40] };
                    }

                    return null;
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[40], behavior.ExpectedPeerTip);
            Assert.Equal(0, this.getHeadersPayloadSentTimes);
            Assert.Equal(result.Consumed, this.headers[40]);

            List<BlockHeader> cacheAfterTipChanged = this.getCachedHeaders(behavior);

            Assert.Equal(10, cacheAfterTipChanged.Count);

            for (int i = 41; i <= 50; i++)
                Assert.Contains(this.headers[i].Header, cacheAfterTipChanged);
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 14 to 15.
        /// <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6. Make sure that cached headers contain no elements,
        /// getHeaders payload was sent to the peer is called and <see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is still 10. Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_NotAbleToConnectCachedHeadersAsync()
        {
            var cache = new List<BlockHeader>() { this.headers[14].Header, this.headers[15].Header };

            ConsensusManagerBehavior behavior = this.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10], NetworkPeerState.HandShaked,
                (presentedHeaders, triggerDownload) =>
                {
                    if (presentedHeaders.First() == this.headers[14].Header)
                    {
                        throw new ConnectHeaderException();
                    }

                    return null;
                });

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(this.headers[10], behavior.ExpectedPeerTip);
            Assert.Equal(1, this.getHeadersPayloadSentTimes);
            Assert.Null(result);

            Assert.Empty(this.getCachedHeaders(behavior));
        }

        /// <summary>
        /// CT is at 5. peer 1 claims block 10 (<see cref="ConsensusManagerBehavior.ExpectedPeerTip"/> is header10). Cached headers have items 11 to 12.
        /// Peer is not attached (attached peer is <c>null</c>). <see cref="ConsensusManagerBehavior.ConsensusTipChangedAsync"/> called with header 6.
        /// Make sure return value is <c>null</c>.
        /// </summary>
        [Fact]
        public async Task ConsensusTipChanged_PeerNotAttachedAsync()
        {
            var cache = new List<BlockHeader>() { this.headers[11].Header, this.headers[12].Header };

            ConsensusManagerBehavior behavior = this.CreateAndAttachBehavior(this.headers[5], cache, this.headers[10]);

            // That will set peer to null.
            behavior.Dispose();

            ConnectNewHeadersResult result = await behavior.ConsensusTipChangedAsync(this.headers[6]);

            Assert.Equal(0, this.getHeadersPayloadSentTimes);
            Assert.Equal(0, this.headersPresentedCalledTimes);
            Assert.Null(result);
        }
    }
}
