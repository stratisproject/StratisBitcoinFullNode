using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Features.FederatedPeg.Interfaces;
using Stratis.Features.FederatedPeg.NetworkHelpers;

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


        public async Task BroadcastAsync(Payload payload)
        {
            // TODO: This needs to be optimised

            List<INetworkPeer> peers = this.connectionManager.ConnectedPeers.ToList();

            var ipAddressComparer = new IPAddressComparer();

            foreach (INetworkPeer peer in peers)
            {
                // Broadcast to peers.
                if (!peer.IsConnected)
                    continue;

                if (this.federatedPegSettings.FederationNodeIpEndPoints.Any(e => ipAddressComparer.Equals(e.Address, peer.PeerEndPoint.Address)))
                {
                    try
                    {
                        await peer.SendMessageAsync(payload).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
        }
    }
}
