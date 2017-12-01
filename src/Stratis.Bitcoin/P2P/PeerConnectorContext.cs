using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Context class for <see cref="PeerConnector"/>.</summary>
    public sealed class PeerConnectorContext
    {
        public PeerConnectorContext(
            IAsyncLoopFactory asyncLoopFactory,
            ILogger logger,
            Network network,
            INetworkPeerFactory networkPeerFactory,
            INodeLifetime nodeLifeTime,
            NodeSettings nodeSettings,
            NetworkPeerConnectionParameters parameters,
            IPeerAddressManager peerAddressManager)
        {
            this.AsyncLoopFactory = asyncLoopFactory;
            this.Network = network;
            this.NodeLifetime = nodeLifeTime;
            this.NodeSettings = nodeSettings;
            this.NetworkPeerFactory = networkPeerFactory;
            this.Parameters = parameters;
            this.PeerAddressManager = peerAddressManager;
        }

        /// <summary>Factory for creating background async loop tasks.</summary>
        public readonly IAsyncLoopFactory AsyncLoopFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>The network the node is running on.</summary>
        public readonly Network Network;

        /// <summary>Global application life cycle control - triggers when application shuts down.</summary>
        public readonly INodeLifetime NodeLifetime;

        /// <summary>User defined node settings.</summary>
        public readonly NodeSettings NodeSettings;

        /// <summary>The network peer parameters that is injected by <see cref="Connection.ConnectionManager"/>.</summary>
        public readonly NetworkPeerConnectionParameters Parameters;

        /// <summary>Peer address manager instance, see <see cref="IPeerAddressManager"/>.</summary>
        public readonly IPeerAddressManager PeerAddressManager;

        /// <summary>Factory for creating P2P network peers.</summary>
        public readonly INetworkPeerFactory NetworkPeerFactory;
    }
}
