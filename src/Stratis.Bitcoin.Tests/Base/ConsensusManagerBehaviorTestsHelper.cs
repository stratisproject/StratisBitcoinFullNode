using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Tests.Base
{
    public class ConsensusManagerBehaviorTestsHelper
    {
        public bool IsIBD = false;

        public bool IsPeerWhitelisted = false;

        public AsyncExecutionEvent<INetworkPeer, NetworkPeerState> StateChanged;
        public AsyncExecutionEvent<INetworkPeer, IncomingMessage> MessageReceived;

        public Mock<INetworkPeer> PeerMock;

        /// <summary>How many times behavior called the <see cref="ConsensusManager.HeadersPresented"/>.</summary>
        public int HeadersPresentedCalledTimes { get; private set; }

        /// <summary>Counter that shows how many times <see cref="GetHeadersPayload"/> was sent to the peer.</summary>
        public int GetHeadersPayloadSentTimes { get; private set; }

        /// <summary>Contains all the <see cref="GetHeadersPayload"/> that were sent to the peer.</summary>
        public List<GetHeadersPayload> GetHeadersPayloadsSent { get; private set; }

        /// <summary>List of <see cref="HeadersPayload"/> that were sent to the peer.</summary>
        public List<HeadersPayload> HeadersPayloadsSent { get; private set; }

        public bool PeerWasBanned => this.testPeerBanning.WasBanningCalled;

        private TestPeerBanning testPeerBanning;

        public ConsensusManagerBehaviorTestsHelper()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
        }

        private readonly ExtendedLoggerFactory loggerFactory;

        /// <summary>Creates the and attaches a new <see cref="ConsensusManagerBehavior"/>.</summary>
        /// <param name="consensusTip">Consensus tip.</param>
        /// <param name="cache">List of cached headers with which behavior is initialized.</param>
        /// <param name="bestReceivedTip">Behavior's expected tip's initial value.</param>
        /// <param name="peerState">Peer connection state returned by the <see cref="INetworkPeer.State"/>.</param>
        /// <param name="connectNewHeadersMethod">Method which is invoked when behavior calls <see cref="IConsensusManager.HeadersPresented"/>.</param>
        /// <returns></returns>
        public ConsensusManagerBehavior CreateAndAttachBehavior(ChainedHeader consensusTip, List<BlockHeader> cache = null,
            ChainedHeader bestReceivedTip = null, NetworkPeerState peerState = NetworkPeerState.HandShaked,
            Func<List<BlockHeader>, bool, ConnectNewHeadersResult> connectNewHeadersMethod = null)
        {
            // Chain
            var chain = new ConcurrentChain(KnownNetworks.StratisMain);
            chain.SetTip(consensusTip);

            // Ibd
            var ibdState = new Mock<IInitialBlockDownloadState>();
            ibdState.Setup(x => x.IsInitialBlockDownload()).Returns(() => this.IsIBD);

            // Consensus manager
            var cmMock = new Mock<IConsensusManager>();

            cmMock.Setup(x => x.HeadersPresented(It.IsAny<INetworkPeer>(), It.IsAny<List<BlockHeader>>(), It.IsAny<bool>()))
                .Returns((INetworkPeer p, List<BlockHeader> presentedHeaders, bool triggerDownload) =>
                {
                    this.HeadersPresentedCalledTimes++;

                    return connectNewHeadersMethod?.Invoke(presentedHeaders, triggerDownload);
                });

            cmMock.Setup(x => x.Tip).Returns(consensusTip);

            this.testPeerBanning = new TestPeerBanning();

            var connectionManagerMock = new Mock<IConnectionManager>();
            connectionManagerMock.SetupGet(x => x.ConnectionSettings).Returns(new ConnectionManagerSettings(new NodeSettings(KnownNetworks.StratisMain)));

            var cmBehavior = new ConsensusManagerBehavior(chain, ibdState.Object, cmMock.Object, this.testPeerBanning, this.loggerFactory);

            // Peer and behavior
            this.PeerMock = this.CreatePeerMock();

            cmBehavior.Attach(this.PeerMock.Object);

            this.PeerMock.Setup(x => x.Behavior<ConsensusManagerBehavior>()).Returns(() => cmBehavior);
            this.PeerMock.Setup(x => x.State).Returns(peerState);

            if (bestReceivedTip != null)
            {
                cmBehavior.SetPrivatePropertyValue(nameof(cmBehavior.BestReceivedTip), bestReceivedTip);
                cmBehavior.SetPrivatePropertyValue(nameof(cmBehavior.BestSentHeader), bestReceivedTip);
            }

            if (cache != null)
                cmBehavior.SetPrivateVariableValue("cachedHeaders", cache);

            this.GetHeadersPayloadSentTimes = 0;
            this.HeadersPayloadsSent = new List<HeadersPayload>();
            this.GetHeadersPayloadsSent = new List<GetHeadersPayload>();

            this.PeerMock.Setup(x => x.SendMessageAsync(It.IsAny<Payload>(), It.IsAny<CancellationToken>())).Returns((Payload payload, CancellationToken token) =>
            {
                if (payload is GetHeadersPayload getHeadersPayload)
                {
                    this.GetHeadersPayloadSentTimes++;
                    this.GetHeadersPayloadsSent.Add(getHeadersPayload);
                }

                if (payload is HeadersPayload headersPayload)
                    this.HeadersPayloadsSent.Add(headersPayload);

                return Task.CompletedTask;
            });

            this.PeerMock.Setup(x => x.SendMessage(It.IsAny<Payload>())).Callback((Payload payload) =>
            {
                if (payload is GetHeadersPayload getHeadersPayload)
                {
                    this.GetHeadersPayloadSentTimes++;
                    this.GetHeadersPayloadsSent.Add(getHeadersPayload);
                }

                if (payload is HeadersPayload headersPayload)
                    this.HeadersPayloadsSent.Add(headersPayload);
            });

            return cmBehavior;
        }

        private Mock<INetworkPeer> CreatePeerMock()
        {
            var peer = new Mock<INetworkPeer>();

            var connection = new NetworkPeerConnection(KnownNetworks.StratisMain, peer.Object, new TcpClient(), 0, (message, token) => Task.CompletedTask,
                new DateTimeProvider(), this.loggerFactory, new PayloadProvider());

            peer.SetupGet(networkPeer => networkPeer.Connection).Returns(connection);

            var connectionParameters = new NetworkPeerConnectionParameters();
            VersionPayload version = connectionParameters.CreateVersion(new IPEndPoint(1, 1), new IPEndPoint(1, 1), KnownNetworks.StratisMain, new DateTimeProvider().GetTimeOffset());
            version.Services = NetworkPeerServices.Network;

            peer.SetupGet(x => x.PeerVersion).Returns(version);
            peer.SetupGet(x => x.State).Returns(NetworkPeerState.HandShaked);

            this.StateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            this.MessageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();

            peer.Setup(x => x.StateChanged).Returns(() => this.StateChanged);
            peer.Setup(x => x.MessageReceived).Returns(() => this.MessageReceived);

            var connectionManagerBehaviorMock = new Mock<IConnectionManagerBehavior>();
            connectionManagerBehaviorMock.Setup(x => x.Whitelisted).Returns(this.IsPeerWhitelisted);

            peer.Setup(x => x.Behavior<IConnectionManagerBehavior>()).Returns(() => connectionManagerBehaviorMock.Object);

            peer.SetupGet(x => x.PeerEndPoint).Returns(new IPEndPoint(1, 1));

            return peer;
        }

        public List<BlockHeader> GetCachedHeaders(ConsensusManagerBehavior behavior)
        {
            return behavior.GetMemberValue("cachedHeaders") as List<BlockHeader>;
        }

        /// <summary>Creates <see cref="GetHeadersPayload"/>.</summary>
        /// <param name="header">Header which is used to create a locator.</param>
        public GetHeadersPayload CreateGetHeadersPayload(ChainedHeader header, uint256 hashStop = null)
        {
            var headersPayload = new GetHeadersPayload()
            {
                BlockLocator = header.GetLocator(),
                HashStop = hashStop
            };

            return headersPayload;
        }

        /// <summary>Simulates receiving a payload from peer.</summary>
        public async Task ReceivePayloadAsync(Payload payload)
        {
            var message = new Message();
            message.Payload = payload;

            // Length of 1 is a bogus value used just to successfully initialize the class.
            await this.MessageReceived.ExecuteCallbacksAsync(this.PeerMock.Object, new IncomingMessage() { Length = 1, Message = message }).ConfigureAwait(false);
        }

        private class TestPeerBanning : IPeerBanning
        {
            public bool WasBanningCalled = false;

            public void BanAndDisconnectPeer(IPEndPoint endpoint, int banTimeSeconds, string reason = null)
            {
                this.WasBanningCalled = true;
            }

            public void BanAndDisconnectPeer(IPEndPoint endpoint, string reason = null)
            {
                this.WasBanningCalled = true;
            }

            public void ClearBannedPeers()
            {
                throw new NotImplementedException();
            }

            public bool IsBanned(IPEndPoint endpoint)
            {
                return this.WasBanningCalled;
            }

            public void UnBanPeer(IPEndPoint endpoint)
            {
                throw new NotImplementedException();
            }
        }
    }
}
