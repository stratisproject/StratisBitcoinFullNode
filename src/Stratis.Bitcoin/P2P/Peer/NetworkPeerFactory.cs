using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// Contract for factory for creating P2P network peers.
    /// </summary>
    public interface INetworkPeerFactory
    {
        /// <summary>
        /// Creates a network peer using already established network connection.
        /// </summary>
        /// <param name="client">Already connected network client.</param>
        /// <param name="parameters">Parameters of the established connection, or <c>null</c> to use default parameters.</param>
        /// <param name="networkPeerDisposer">Maintains a list of connected peers and ensures their proper disposal. Or <c>null</c> if case disposal should be handled from user code.</param>
        /// <returns>New network peer that is connected via the established connection.</returns>
        INetworkPeer CreateNetworkPeer(TcpClient client, NetworkPeerConnectionParameters parameters = null, NetworkPeerDisposer networkPeerDisposer = null);

        /// <summary>
        /// Creates a new network peer which is connected to a specified counterparty.
        /// </summary>
        /// <param name="endPoint">Address and port of the counterparty to connect to.</param>
        /// <param name="myVersion">Version of the protocol that the node supports.</param>
        /// <param name="isRelay">Whether the remote peer should announce relayed transactions or not. See <see cref="VersionPayload.Relay"/> for more information.</param>
        /// <param name="cancellation">Cancallation token that allows to interrupt establishing of the connection.</param>
        /// <param name="networkPeerDisposer">Maintains a list of connected peers and ensures their proper disposal. Or <c>null</c> if case disposal should be handled from user code.</param>
        /// <returns>Network peer connected to the specified counterparty.</returns>
        Task<INetworkPeer> CreateConnectedNetworkPeerAsync(string endPoint, ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION, bool isRelay = true, CancellationToken cancellation = default(CancellationToken), NetworkPeerDisposer networkPeerDisposer = null);

        /// <summary>
        /// Creates a new network peer which is connected to a specified counterparty.
        /// </summary>
        /// <param name="peerEndPoint">Address and port of the counterparty to connect to.</param>
        /// <param name="parameters">Parameters specifying how the connection with the counterparty should be established, or <c>null</c> to use default parameters.</param>
        /// <param name="networkPeerDisposer">Maintains a list of connected peers and ensures their proper disposal. Or <c>null</c> if case disposal should be handled from user code.</param>
        /// <returns>Network peer connected to the specified counterparty.</returns>
        Task<INetworkPeer> CreateConnectedNetworkPeerAsync(IPEndPoint peerEndPoint, NetworkPeerConnectionParameters parameters = null, NetworkPeerDisposer networkPeerDisposer = null);

        /// <summary>
        /// Creates a new network peer server.
        /// <para>When created, the server is ready to be started, but this method does not start listening.</para>
        /// </summary>
        /// <param name="localEndPoint">IP address and port to listen on.</param>
        /// <param name="externalEndPoint">IP address and port that the server is reachable from the Internet on.</param>
        /// <param name="version">Version of the network protocol that the server should run.</param>
        /// <returns>Newly created network peer server, which is ready to be started.</returns>
        NetworkPeerServer CreateNetworkPeerServer(IPEndPoint localEndPoint, IPEndPoint externalEndPoint, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION);

        /// <summary>
        /// Creates a new representation of the network connection using TCP client object.
        /// </summary>
        /// <param name="peer">Network peer the node is connected to, or will connect to.</param>
        /// <param name="client">Initialized and possibly connected TCP client to the peer.</param>
        /// <param name="processMessageAsync">Callback to be called when a new message arrives from the peer.</param>
        NetworkPeerConnection CreateNetworkPeerConnection(INetworkPeer peer, TcpClient client, ProcessMessageAsync<IncomingMessage> processMessageAsync);

        /// <summary>
        /// Registers a callback that will be passed to all created peers. It gets called prior to sending messages to the peer.
        /// </summary>
        /// <param name="callback">The callback to be used by each peer.</param>
        void RegisterOnSendingMessageCallback(Action<IPEndPoint, Payload> callback);
    }

    /// <summary>
    /// Factory for creating P2P network peers.
    /// </summary>
    public class NetworkPeerFactory : INetworkPeerFactory
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>A provider of network payload messages.</summary>
        private readonly PayloadProvider payloadProvider;

        private readonly ISelfEndpointTracker selfEndpointTracker;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        private readonly Network network;

        /// <summary>Identifier of the last network peer client this factory produced.</summary>
        /// <remarks>When a new client is created, the ID is incremented so that each client has its own unique ID.</remarks>
        private int lastClientId;

        /// <summary>Provider of IBD state.</summary>
        private readonly IInitialBlockDownloadState initialBlockDownloadState;

        /// <summary>Configuration related to incoming and outgoing connections.</summary>
        private readonly ConnectionManagerSettings connectionManagerSettings;

        /// <summary>Callback that is invoked just before a message is to be sent to a peer, or <c>null</c> when nothing needs to be called.</summary>
        private Action<IPEndPoint, Payload> onSendingMessage;

        /// <summary>
        /// Initializes a new instance of the factory.
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        /// <param name="payloadProvider">A provider of network payload messages.</param>
        /// <param name="selfEndpointTracker">Tracker for endpoints known to be self.</param>
        /// <param name="initialBlockDownloadState">Provider of IBD state.</param>
        /// <param name="connectionManagerSettings">Configuration related to incoming and outgoing connections.</param>
        public NetworkPeerFactory(Network network,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            PayloadProvider payloadProvider,
            ISelfEndpointTracker selfEndpointTracker,
            IInitialBlockDownloadState initialBlockDownloadState,
            ConnectionManagerSettings connectionManagerSettings)
        {
            Guard.NotNull(network, nameof(network));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));
            Guard.NotNull(loggerFactory, nameof(loggerFactory));

            this.network = network;
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.payloadProvider = payloadProvider;
            this.selfEndpointTracker = selfEndpointTracker;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.lastClientId = 0;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.connectionManagerSettings = connectionManagerSettings;
        }

        /// <inheritdoc/>
        public INetworkPeer CreateNetworkPeer(TcpClient client, NetworkPeerConnectionParameters parameters = null, NetworkPeerDisposer networkPeerDisposer = null)
        {
            Guard.NotNull(client, nameof(client));

            Action<INetworkPeer> onDisconnected = null;

            if (networkPeerDisposer != null)
                onDisconnected = networkPeerDisposer.OnPeerDisconnectedHandler;

            var peer = new NetworkPeer((IPEndPoint)client.Client.RemoteEndPoint, this.network, parameters, client, this.dateTimeProvider, this, this.loggerFactory, this.selfEndpointTracker, onDisconnected, this.onSendingMessage);

            networkPeerDisposer?.AddPeer(peer);

            return peer;
        }

        /// <inheritdoc/>
        public async Task<INetworkPeer> CreateConnectedNetworkPeerAsync(
            string endPoint,
            ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION,
            bool isRelay = true,
            CancellationToken cancellation = default(CancellationToken),
            NetworkPeerDisposer networkPeerDisposer = null)
        {
            Guard.NotNull(endPoint, nameof(endPoint));

            IPEndPoint ipEndPoint = Utils.ParseIpEndpoint(endPoint, this.network.DefaultPort);
            var parameters = new NetworkPeerConnectionParameters()
            {
                ConnectCancellation = cancellation,
                IsRelay = isRelay,
                Version = myVersion,
                Services = NetworkPeerServices.Nothing,
            };

            return await this.CreateConnectedNetworkPeerAsync(ipEndPoint, parameters, networkPeerDisposer);
        }

        /// <inheritdoc/>
        public async Task<INetworkPeer> CreateConnectedNetworkPeerAsync(
            IPEndPoint peerEndPoint,
            NetworkPeerConnectionParameters parameters = null,
            NetworkPeerDisposer networkPeerDisposer = null)
        {
            Guard.NotNull(peerEndPoint, nameof(peerEndPoint));

            Action<INetworkPeer> onDisconnected = null;

            if (networkPeerDisposer != null)
                onDisconnected = networkPeerDisposer.OnPeerDisconnectedHandler;

            var peer = new NetworkPeer(peerEndPoint, this.network, parameters, this, this.dateTimeProvider, this.loggerFactory, this.selfEndpointTracker, onDisconnected, this.onSendingMessage);

            try
            {
                await peer.ConnectAsync(peer.ConnectionParameters.ConnectCancellation).ConfigureAwait(false);

                networkPeerDisposer?.AddPeer(peer);
            }
            catch
            {
                peer.Dispose();
                throw;
            }

            return peer;
        }

        /// <inheritdoc/>
        public NetworkPeerServer CreateNetworkPeerServer(IPEndPoint localEndPoint, IPEndPoint externalEndPoint, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION)
        {
            Guard.NotNull(localEndPoint, nameof(localEndPoint));
            Guard.NotNull(externalEndPoint, nameof(externalEndPoint));

            return new NetworkPeerServer(this.network, localEndPoint, externalEndPoint, version, this.loggerFactory, this, this.initialBlockDownloadState, this.connectionManagerSettings);
        }

        /// <inheritdoc/>
        public NetworkPeerConnection CreateNetworkPeerConnection(INetworkPeer peer, TcpClient client, ProcessMessageAsync<IncomingMessage> processMessageAsync)
        {
            Guard.NotNull(peer, nameof(peer));
            Guard.NotNull(client, nameof(client));
            Guard.NotNull(processMessageAsync, nameof(processMessageAsync));

            int id = Interlocked.Increment(ref this.lastClientId);
            return new NetworkPeerConnection(this.network, peer, client, id, processMessageAsync, this.dateTimeProvider, this.loggerFactory, this.payloadProvider);
        }

        /// <inheritdoc/>
        public void RegisterOnSendingMessageCallback(Action<IPEndPoint, Payload> callback)
        {
            this.onSendingMessage = callback;
        }
    }
}