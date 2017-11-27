using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// Contract for factory for creating P2P network peer clients and servers.
    /// </summary>
    public interface INetworkPeerFactory
    {
        NetworkPeer CreateNetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, Socket socket, VersionPayload peerVersion);

        NetworkPeerServer CreateNetworkPeerServer(Network network, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, int internalPort = -1);
    }

    /// <summary>
    /// Factory for creating P2P network peer clients and servers.
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

        /// <summary>
        /// Creates a network peer using already established network connection.
        /// </summary>
        /// <param name="peerAddress">Address of the connected counterparty.</param>
        /// <param name="network">The network to connect to.</param>
        /// <param name="parameters">Parameters of the established connection.</param>
        /// <param name="socket">Socket representing the established network connection.</param>
        /// <param name="peerVersion">Version of the connected counterparty.</param>
        /// <returns>New network peer that is connected via the established connection.</returns>
        public NetworkPeer CreateNetworkPeer(NetworkAddress peerAddress, Network network, NetworkPeerConnectionParameters parameters, Socket socket, VersionPayload peerVersion)
        {
            return new NetworkPeer(peerAddress, network, parameters, socket, peerVersion, this.dateTimeProvider, this.loggerFactory);
        }

        /// <summary>
        /// Connect to a random node on the network.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="parameters">Parameters for the connection that to be established.</param>
        /// <param name="connectedEndpoints">The already connected endpoints, a new endpoint will be selected outside of existing groups.</param>
        /// <returns>New network peer that is connected to a random node on the network.</returns>
        public NetworkPeer CreateConnectedNetworkPeer(Network network, NetworkPeerConnectionParameters parameters = null, IPEndPoint[] connectedEndpoints = null)
        {
            parameters = parameters ?? new NetworkPeerConnectionParameters();
            return this.CreateConnectedNetworkPeerWithDiscovery(network, parameters, connectedEndpoints);
        }

        /// <summary>
        /// Connect to a random node on the network.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="parameters">Parameters for the connection to be established.</param>
        /// <param name="connectedEndpoints">The already connected endpoints, the new endpoint will be select outside of existing groups.</param>
        /// <param name="getGroup">Group selector, by default <see cref="IpExtensions.GetGroup(IPAddress)"/> is used.</param>
        /// <returns>New network peer that is connected to a random node on the network.</returns>
        public NetworkPeer CreateConnectedNetworkPeerWithDiscovery(Network network, NetworkPeerConnectionParameters parameters = null, IPEndPoint[] connectedEndpoints = null, Func<IPEndPoint, byte[]> getGroup = null)
        {
            return this.CreateConnectedNetworkPeerWithDiscovery(network, parameters, new Func<IPEndPoint[]>(() => connectedEndpoints), getGroup);
        }

        /// <summary>
        /// [Deprecated] This whole connect method will be deprecated
        /// 
        /// Connect to a random node on the network.
        /// </summary>
        /// <param name="network">The network to connect to.</param>
        /// <param name="parameters">Parameters for the connection to be established.</param>
        /// <param name="connectedEndpoints">The already connected endpoints, the new endpoint will be select outside of existing groups.</param>
        /// <param name="getGroup">Group selector, by default <see cref="IpExtensions.GetGroup(IPAddress)"/> is used.</param>
        /// <returns></returns>
        public NetworkPeer CreateConnectedNetworkPeerWithDiscovery(Network network, NetworkPeerConnectionParameters parameters, Func<IPEndPoint[]> connectedEndpoints, Func<IPEndPoint, byte[]> getGroup = null)
        {
            getGroup = getGroup ?? new Func<IPEndPoint, byte[]>((a) => IpExtensions.GetGroup(a.Address));
            if (connectedEndpoints() == null)
                connectedEndpoints = new Func<IPEndPoint[]>(() => new IPEndPoint[0]);

            parameters = parameters ?? new NetworkPeerConnectionParameters();

            //[Deprecated] This whole connect method will be deprecated
            //AddressManagerBehavior addrmanBehavior = parameters.TemplateBehaviors.FindOrCreate(() => new AddressManagerBehavior(new AddressManager()));
            //AddressManager addrman = AddressManagerBehavior.GetAddrman(parameters);

            DateTimeOffset start = DateTimeOffset.UtcNow;
            while (true)
            {
                parameters.ConnectCancellation.ThrowIfCancellationRequested();

                //[Deprecated] This whole connect method will be deprecated
                //if ((addrman.Count == 0) || ((DateTimeOffset.UtcNow - start) > TimeSpan.FromSeconds(60)))
                //{
                //    addrmanBehavior.DiscoverPeers(network, parameters);
                //    start = DateTimeOffset.UtcNow;
                //}

                NetworkAddress addr = null;
                int groupFail = 0;
                while (true)
                {
                    if (groupFail > 50)
                    {
                        parameters.ConnectCancellation.WaitHandle.WaitOne((int)TimeSpan.FromSeconds(60).TotalMilliseconds);
                        break;
                    }

                    //[Deprecated] This whole connect method will be deprecated
                    //addr = addrman.Select();
                    if (addr == null)
                    {
                        parameters.ConnectCancellation.WaitHandle.WaitOne(1000);
                        break;
                    }

                    if (!addr.Endpoint.Address.IsValid())
                        continue;

                    bool groupExist = connectedEndpoints().Any(a => getGroup(a).SequenceEqual(getGroup(addr.Endpoint)));
                    if (groupExist)
                    {
                        groupFail++;
                        continue;
                    }

                    break;
                }

                if (addr == null)
                    continue;

                try
                {
                    var timeout = new CancellationTokenSource(5000);
                    NetworkPeerConnectionParameters param2 = parameters.Clone();
                    param2.ConnectCancellation = CancellationTokenSource.CreateLinkedTokenSource(parameters.ConnectCancellation, timeout.Token).Token;
                    NetworkPeer node = Connect(network, addr.Endpoint, param2);
                    return node;
                }
                catch (OperationCanceledException ex)
                {
                    if (ex.CancellationToken == parameters.ConnectCancellation)
                        throw;
                }
                catch (SocketException)
                {
                    parameters.ConnectCancellation.WaitHandle.WaitOne(500);
                }
            }
        }

        /// <summary>
        /// Connect to the node of this machine,
        /// </summary>
        /// <param name="network"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static NetworkPeer ConnectToLocal(Network network, NetworkPeerConnectionParameters parameters)
        {
            return Connect(network, Utils.ParseIpEndpoint("localhost", network.DefaultPort), parameters);
        }

        public static NetworkPeer ConnectToLocal(Network network, ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION, bool isRelay = true, CancellationToken cancellation = default(CancellationToken))
        {
            return ConnectToLocal(network, new NetworkPeerConnectionParameters()
            {
                ConnectCancellation = cancellation,
                IsRelay = isRelay,
                Version = myVersion
            });
        }

        public static NetworkPeer Connect(Network network, string endpoint, NetworkPeerConnectionParameters parameters)
        {
            return Connect(network, Utils.ParseIpEndpoint(endpoint, network.DefaultPort), parameters);
        }

        public static NetworkPeer Connect(Network network, string endpoint, ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION, bool isRelay = true, CancellationToken cancellation = default(CancellationToken))
        {
            return Connect(network, Utils.ParseIpEndpoint(endpoint, network.DefaultPort), myVersion, isRelay, cancellation);
        }

        public static NetworkPeer Connect(Network network, NetworkAddress endpoint, NetworkPeerConnectionParameters parameters)
        {
            return new NetworkPeer(endpoint, network, parameters);
        }

        public static NetworkPeer Connect(Network network, IPEndPoint endpoint, NetworkPeerConnectionParameters parameters)
        {
            var peer = new NetworkAddress()
            {
                Time = DateTimeOffset.UtcNow,
                Endpoint = endpoint
            };

            return new NetworkPeer(peer, network, parameters);
        }

        public static NetworkPeer Connect(Network network, IPEndPoint endpoint, ProtocolVersion myVersion = ProtocolVersion.PROTOCOL_VERSION, bool isRelay = true, CancellationToken cancellation = default(CancellationToken))
        {
            return Connect(network, endpoint, new NetworkPeerConnectionParameters()
            {
                ConnectCancellation = cancellation,
                IsRelay = isRelay,
                Version = myVersion,
                Services = NodeServices.Nothing,
            });
        }

        public NetworkPeerServer CreateNetworkPeerServer(Network network, ProtocolVersion version = ProtocolVersion.PROTOCOL_VERSION, int internalPort = -1)
        {
            return new NetworkPeerServer(network, version, internalPort, this.dateTimeProvider, this.loggerFactory, this);
        }

    }
}
