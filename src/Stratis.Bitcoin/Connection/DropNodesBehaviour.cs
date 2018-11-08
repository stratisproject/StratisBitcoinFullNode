using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.P2P;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.Connection
{
    /// <summary>
    /// If the light wallet is only connected to nodes behind
    /// it cannot progress progress to the tip to get the full balance
    /// this behaviour will make sure place is kept for nodes higher then
    /// current tip.
    /// </summary>
    public class DropNodesBehaviour : NetworkPeerBehavior
    {
        /// <summary>Logger factory to create loggers.</summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        private readonly ConcurrentChain chain;

        private readonly IConnectionManager connection;

        private readonly decimal dropThreshold;

        public DropNodesBehaviour(ConcurrentChain chain, IConnectionManager connectionManager, ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName, $"[{this.GetHashCode():x}] ");
            this.loggerFactory = loggerFactory;

            this.chain = chain;
            this.connection = connectionManager;

            // 80% of current max connections, the last 20% will only
            // connect to nodes ahead of the current best chain.
            this.dropThreshold = 0.8M;
        }

        private Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            if (message.Message.Payload is VersionPayload version)
            {
                IPeerConnector peerConnector = null;
                if (this.connection.ConnectionSettings.Connect.Any())
                    peerConnector = this.connection.PeerConnectors.First(pc => pc is PeerConnectorConnectNode);
                else
                    peerConnector = this.connection.PeerConnectors.First(pc => pc is PeerConnectorDiscovery);

                // Find how much 20% max nodes.
                decimal thresholdCount = Math.Round(peerConnector.MaxOutboundConnections * this.dropThreshold, MidpointRounding.ToEven);

                if (thresholdCount < this.connection.ConnectedPeers.Count())
                {
                    if (version.StartHeight < this.chain.Height)
                        peer.Disconnect($"Node at height = {version.StartHeight} too far behind current height");
                }
            }

            return Task.CompletedTask;
        }

        [NoTrace]
        protected override void AttachCore()
        {
            this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
        }

        [NoTrace]
        protected override void DetachCore()
        {
            this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
        }

        [NoTrace]
        public override object Clone()
        {
            return new DropNodesBehaviour(this.chain, this.connection, this.loggerFactory);
        }
    }
}