using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.Connection
{
    public interface IConnectionManager : IDisposable
    {
        /// <summary>
        /// Adds a peer to the address manager's collection as well as
        /// the connection manager's add node collection.
        /// </summary>
        void AddNodeAddress(IPEndPoint ipEndpoint);

        /// <summary>
        /// Adds a peer to the address manager's connected nodes collection.
        /// <para>
        /// This list is inspected by the peer connectors to determine if the peer
        /// isn't already connected.
        /// </para>
        /// </summary>
        void AddConnectedPeer(INetworkPeer peer);

        void AddDiscoveredNodesRequirement(NetworkPeerServices services);

        Task<INetworkPeer> ConnectAsync(IPEndPoint ipEndpoint);

        IReadOnlyNetworkPeerCollection ConnectedPeers { get; }

        INetworkPeer FindLocalNode();

        INetworkPeer FindNodeByEndpoint(IPEndPoint ipEndpoint);

        INetworkPeer FindNodeByIp(IPAddress ipAddress);

        INetworkPeer FindNodeById(int peerId);

        void RemoveConnectedPeer(INetworkPeer peer, string reason);

        /// <summary>Notifies other components about peer being disconnected.</summary>
        void PeerDisconnected(int networkPeerId);

        /// <summary>Initializes and starts each peer connection as well as peer discovery.</summary>
        void Initialize(IConsensusManager consensusManager);

        /// <summary>The network the node is running on.</summary>
        Network Network { get; }

        /// <summary>Factory for creating P2P network peers.</summary>
        INetworkPeerFactory NetworkPeerFactory { get; }

        /// <summary>User defined node settings.</summary>
        NodeSettings NodeSettings { get; }

        /// <summary>The network peer parameters for the <see cref="IConnectionManager"/>.</summary>
        NetworkPeerConnectionParameters Parameters { get; }

        /// <summary>Includes the add node, connect and discovery peer connectors.</summary>
        IEnumerable<IPeerConnector> PeerConnectors { get; }

        /// <summary>Connection settings.</summary>
        ConnectionManagerSettings ConnectionSettings { get; }

        /// <summary>
        /// Remove a peer from the address manager's collection as well as
        /// the connection manager's add node collection.
        /// </summary>
        void RemoveNodeAddress(IPEndPoint ipEndpoint);

        List<NetworkPeerServer> Servers { get; }
    }
}