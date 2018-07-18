using System;
using System.Collections.Generic;
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

namespace Stratis.Bitcoin.Tests.Base
{
    public class ConsensusManagerBehaviorTestsHelper
    {
        public bool IsIBD = false;

        public AsyncExecutionEvent<INetworkPeer, NetworkPeerState> StateChanged;
        public AsyncExecutionEvent<INetworkPeer, IncomingMessage> MessageReceived;

        public Mock<INetworkPeer> PeerMock;

        /// <summary>How many times behavior called the <see cref="ConsensusManager.HeadersPresented"/>.</summary>
        public int HeadersPresentedCalledTimes;

        /// <summary>Counter that shows how many times <see cref="GetHeadersPayload"/> was sent to the peer.</summary>
        public int GetHeadersPayloadSentTimes;

        public ConsensusManagerBehaviorTestsHelper()
        {
            this.loggerFactory = new ExtendedLoggerFactory();
            this.loggerFactory.AddConsoleWithFilters();
        }

        private readonly ExtendedLoggerFactory loggerFactory;

        /// <summary>Creates the and attaches a new <see cref="ConsensusManagerBehavior"/>.</summary>
        /// <param name="consensusTip">Consensus tip.</param>
        /// <param name="cache">List of cached headers with which behavior is initialized.</param>
        /// <param name="expectedPeerTip">Behavior's expected tip.</param>
        /// <param name="peerState">Peer connection state.</param>
        /// <param name="connectNewHeadersMethod">Method which is invoked when behavior calls CM.HeadersPresented.</param>
        /// <returns></returns>
        public async Task<ConsensusManagerBehavior> CreateAndAttachBehaviorAsync(ChainedHeader consensusTip, List<BlockHeader> cache = null,
            ChainedHeader expectedPeerTip = null, NetworkPeerState peerState = NetworkPeerState.HandShaked,
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
                    this.HeadersPresentedCalledTimes++;

                    return connectNewHeadersMethod?.Invoke(presentedHeaders, triggerDownload);
                });

            cmMock.Setup(x => x.Tip).Returns(consensusTip);

            var cmBehavior = new ConsensusManagerBehavior(chain, ibdState.Object, cmMock.Object, new Mock<IPeerBanning>().Object,
                new Mock<IConnectionManager>().Object, this.loggerFactory);

            // Peer and behavior
            this.PeerMock = this.CreatePeerMock();

            cmBehavior.Attach(this.PeerMock.Object);

            await Task.Delay(500);

            this.PeerMock.Setup(x => x.Behavior<ConsensusManagerBehavior>()).Returns(() => cmBehavior);
            this.PeerMock.Setup(x => x.State).Returns(peerState);

            if (expectedPeerTip != null)
                cmBehavior.SetPrivatePropertyValue("ExpectedPeerTip", expectedPeerTip);

            if (cache != null)
                cmBehavior.SetPrivateVariableValue("cachedHeaders", cache);

            this.GetHeadersPayloadSentTimes = 0;

            this.PeerMock.Setup(x => x.SendMessageAsync(It.IsAny<Payload>(), It.IsAny<CancellationToken>())).Returns((Payload payload, CancellationToken token) =>
            {
                if (payload is GetHeadersPayload)
                    this.GetHeadersPayloadSentTimes++;

                return Task.CompletedTask;
            });

            return cmBehavior;
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

            this.StateChanged = new AsyncExecutionEvent<INetworkPeer, NetworkPeerState>();
            this.MessageReceived = new AsyncExecutionEvent<INetworkPeer, IncomingMessage>();

            peer.Setup(x => x.StateChanged).Returns(() => this.StateChanged);
            peer.Setup(x => x.MessageReceived).Returns(() => this.MessageReceived);

            return peer;
        }

        public List<BlockHeader> GetCachedHeaders(ConsensusManagerBehavior behavior)
        {
            return behavior.GetMemberValue("cachedHeaders") as List<BlockHeader>;
        }
    }
}
