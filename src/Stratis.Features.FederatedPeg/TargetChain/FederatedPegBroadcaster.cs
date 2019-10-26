using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.TargetChain
{
    public class FederatedPegBroadcaster : IFederatedPegBroadcaster
    {
        private readonly IConnectionManager connectionManager;
        private readonly IFederatedPegSettings federatedPegSettings;

        public FederatedPegBroadcaster(
            IConnectionManager connectionManager,
            IFederatedPegSettings federatedPegSettings)
        {
            this.connectionManager = connectionManager;
            this.federatedPegSettings = federatedPegSettings;
        }

        /// <inheritdoc />
        public async Task BroadcastAsync(Payload payload)
        {
            IEnumerable<INetworkPeer> connectedPeers = this.connectionManager.ConnectedPeers
                .Where(peer => (peer?.IsConnected ?? false) && this.federatedPegSettings.FederationNodeIpAddresses.Contains(peer.PeerEndPoint.Address));

            Parallel.ForEach<INetworkPeer>(connectedPeers, async (INetworkPeer peer) =>
            {
                try
                {
                    await peer.SendMessageAsync(payload).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            });
        }
    }
}
