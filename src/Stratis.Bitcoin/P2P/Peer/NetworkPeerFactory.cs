using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
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
        /// <param name="peerAddress">Address of the connected counterparty.</param>
        /// <param name="network">The network to connect to.</param>
        /// <param name="parameters">Parameters of the established connection.</param>
        /// <param name="socket">Socket representing the established network connection.</param>
        /// <param name="peerVersion">Version of the connected counterparty.</param>
        /// <returns>New network peer that is connected via the established connection.</returns>
        NetworkPeer CreateNetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, Socket socket, VersionPayload peerVersion);

        /// <summary>
        /// Creates a new network peer which is connected to a specified counterparty.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="endpoint">Address of the counterparty to connect to.</param>
        /// <param name="myVersion">Version of the protocol that the node supports.</param>
        /// <param name="isRelay">Whether the remote peer should announce relayed transactions or not. See <see cref="VersionPayload.Relay"/> for more information.</param>
        /// <param name="cancellation">Cancallation token that allows to interrupt establishing of the connection.</param>
        /// <returns>Network peer connected to the specified counterparty.</returns>
        NetworkPeer CreateConnectedNetworkPeer(Network network, string endpoint, ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION, bool isRelay = true, CancellationToken cancellation = default(CancellationToken));

        /// <summary>
        /// Creates a new network peer which is connected to a specified counterparty.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="endpoint">Address of the counterparty to connect to.</param>
        /// <param name="parameters">Parameters specifying how the connection with the counterparty should be established.</param>
        /// <returns>Network peer connected to the specified counterparty.</returns>
        NetworkPeer CreateConnectedNetworkPeer(Network network, string endpoint, NetworkPeerConnectionParameters parameters);

        /// <summary>
        /// Creates a new network peer which is connected to a specified counterparty.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="endpoint">Address of the counterparty to connect to.</param>
        /// <param name="parameters">Parameters specifying how the connection with the counterparty should be established.</param>
        /// <returns>Network peer connected to the specified counterparty.</returns>
        NetworkPeer CreateConnectedNetworkPeer(Network network, NetworkAddress endpoint, NetworkPeerConnectionParameters parameters);

        /// <summary>
        /// Creates a new network peer which is connected to a specified counterparty.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="endpoint">Address of the counterparty to connect to.</param>
        /// <param name="parameters">Parameters specifying how the connection with the counterparty should be established.</param>
        /// <returns>Network peer connected to the specified counterparty.</returns>
        NetworkPeer CreateConnectedNetworkPeer(Network network, IPEndPoint endpoint, NetworkPeerConnectionParameters parameters);

        /// <summary>
        /// Creates a new network peer server.
        /// <para>When created, the server is ready to be started, but this method does not start listening.</para>
        /// </summary>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="version">Version of the network protocol that the server should run.</param>
        /// <param name="internalPort">Port on which the server will listen, or -1 to use the default port for the selected network.</param>
        /// <returns>Newly created network peer server, which is ready to be started.</returns>
        NetworkPeerServer CreateNetworkPeerServer(Network network, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, int internalPort = -1);
    }

    /// <summary>
    /// Factory for creating P2P network peers.
    /// </summary>
    public class NetworkPeerFactory : INetworkPeerFactory
    {
        /// <summary>Factory for creating loggers.</summary>
        private readonly ILoggerFactory loggerFactory;
        
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provider of time functions.</summary>
        private readonly IDateTimeProvider dateTimeProvider;

        /// <summary>
        /// Initializes a new instance of the factory.
        /// </summary>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public NetworkPeerFactory(IDateTimeProvider dateTimeProvider, ILoggerFactory loggerFactory)
        {
            this.dateTimeProvider = dateTimeProvider;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <inheritdoc/>
        public NetworkPeer CreateNetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, Socket socket, VersionPayload peerVersion)
        {
            return new NetworkPeer(peerAddress, network, parameters, socket, peerVersion, this.dateTimeProvider, this.loggerFactory);
        }

        /// <inheritdoc/>
        public NetworkPeer CreateConnectedNetworkPeer(Network network, string endpoint, ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION, bool isRelay = true, CancellationToken cancellation = default(CancellationToken))
        {
            IPEndPoint ipEndPoint = Utils.ParseIpEndpoint(endpoint, network.DefaultPort);
            var parameters = new NetworkPeerConnectionParameters()
            {
                ConnectCancellation = cancellation,
                IsRelay = isRelay,
                Version = myVersion,
                Services = NetworkPeerServices.Nothing,
            };

            return this.CreateConnectedNetworkPeer(network, ipEndPoint, parameters);
        }

        /// <inheritdoc/>
        public NetworkPeer CreateConnectedNetworkPeer(Network network, string endpoint, NetworkPeerConnectionParameters parameters)
        {
            return this.CreateConnectedNetworkPeer(network, Utils.ParseIpEndpoint(endpoint, network.DefaultPort), parameters);
        }

        /// <inheritdoc/>
        public NetworkPeer CreateConnectedNetworkPeer(Network network, IPEndPoint endpoint, NetworkPeerConnectionParameters parameters)
        {
            var peerAddress = new NetworkAddress()
            {
                Time = this.dateTimeProvider.GetTimeOffset(),
                Endpoint = endpoint
            };

            return this.CreateConnectedNetworkPeer(network, peerAddress, parameters);
        }

        /// <inheritdoc/>
        public NetworkPeer CreateConnectedNetworkPeer(Network network, NetworkAddress endpoint, NetworkPeerConnectionParameters parameters)
        {
            return new NetworkPeer(endpoint, network, parameters, this.dateTimeProvider, this.loggerFactory);
        }

        /// <inheritdoc/>
        public NetworkPeerServer CreateNetworkPeerServer(Network network, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, int internalPort = -1)
        {
            return new NetworkPeerServer(network, version, internalPort, this.dateTimeProvider, this.loggerFactory, this);
        }
    }
}
