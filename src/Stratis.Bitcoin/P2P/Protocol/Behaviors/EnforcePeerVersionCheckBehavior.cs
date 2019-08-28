using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P.Protocol.Behaviors
{
    /// <summary>
    /// Sets the minimum supported client version <see cref="this.NodeSettings.MinProtocolVersion"> to <see cref="this.Network.Consensus.Options.EnforcedMinProtocolVersion">
    /// based on the predefined block height <see cref="this.Network.Consensus.Options.EnforceMinProtocolVersionAtBlockHeight">.
    /// Once the new minimum supported client version is changed all existing peer connections will be dropped upon the first received message from outdated client.
    /// </summary>
    public class EnforcePeerVersionCheckBehavior : NetworkPeerBehavior
    {
        /// <summary>An indexer that provides methods to query the best chain (the chain that is validated by the full consensus rules)</summary>
        protected ChainIndexer ChainIndexer { get; private set; }

        /// <summary>User defined node settings.</summary>
        protected NodeSettings NodeSettings { get; private set; }

        /// <summary>Specification of the network the node runs on - regtest/testnet/mainnet.</summary>
        protected Network Network { get; private set; }

        /// <summary>Logger factory usded while cloning the object.</summary>
        private ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Set to <c>true</c> if the attached peer callbacks have been registered and they should be unregistered,
        /// <c>false</c> if the callbacks are not registered.
        /// </summary>
        private bool callbacksRegistered;

        /// <summary>
        /// Initializes an instance of the object for outbound network peers.
        /// </summary>
        /// <param name="chainIndexer">The chain of blocks.</param>
        /// <param name="nodeSettings">User defined node settings.</param>
        /// <param name="network">Specification of the network the node runs on - regtest/testnet/mainnet.</param>
        /// <param name="loggerFactory">Factory for creating loggers.</param>
        public EnforcePeerVersionCheckBehavior(ChainIndexer chainIndexer,
            NodeSettings nodeSettings,
            Network network,
            ILoggerFactory loggerFactory)
        {
            this.ChainIndexer = chainIndexer;
            this.NodeSettings = nodeSettings;
            this.Network = network;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
        }

        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            int enforceMinProtocolVersionAtBlockHeight = this.Network.Consensus.Options.EnforceMinProtocolVersionAtBlockHeight;

            bool enforcementRequired = enforceMinProtocolVersionAtBlockHeight > 0;
            bool enforcedAlready = this.NodeSettings.MinProtocolVersion >= this.Network.Consensus.Options.EnforcedMinProtocolVersion;
            bool enforcementHeightReached = this.ChainIndexer.Height >= enforceMinProtocolVersionAtBlockHeight;
            if (enforcementRequired && !enforcedAlready && enforcementHeightReached)
            {
                this.logger.LogDebug("Changing the minumum supported protocol version from {0} to {1}.", this.NodeSettings.MinProtocolVersion, this.Network.Consensus.Options.EnforcedMinProtocolVersion);
                this.NodeSettings.MinProtocolVersion = this.Network.Consensus.Options.EnforcedMinProtocolVersion;
            }

            // The statement below will close connections in case the this.NodeSettings.MinProtocolVersion has changed during node execution.
            if (peer.PeerVersion.Version < this.NodeSettings.MinProtocolVersion)
            {
                this.logger.LogError("Unsupported client version, dropping connection.");
                this.AttachedPeer.Disconnect("Peer is using unsupported client version");
            }

            return Task.CompletedTask;
        }

        [NoTrace]
        protected override void AttachCore()
        {
            if (this.AttachedPeer != null)
                return;

            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
            this.callbacksRegistered = true;
        }

        [NoTrace]
        protected override void DetachCore()
        {
            if (this.callbacksRegistered)
            {
                this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
            }
        }

        [NoTrace]
        public override object Clone()
        {
            return new EnforcePeerVersionCheckBehavior(this.ChainIndexer, this.NodeSettings, this.Network, this.loggerFactory);
        }
    }
}