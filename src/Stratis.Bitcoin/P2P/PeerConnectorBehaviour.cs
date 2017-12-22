using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Maintain connection to a given set of peers.</summary>
    internal sealed class PeerConnectorBehaviour : NetworkPeerBehavior
    {
        /// <summary>The peer connector this behaviour relates to.</summary>
        private readonly IPeerConnector peerConnector;

        internal PeerConnectorBehaviour(IPeerConnector peerConnector)
        {
            this.peerConnector = peerConnector;
        }

        /// <inheritdoc/>
        protected override void AttachCore()
        {
            this.AttachedPeer.StateChanged += this.AttachedPeer_StateChanged;
        }

        /// <inheritdoc/>
        protected override void DetachCore()
        {
            this.AttachedPeer.StateChanged -= this.AttachedPeer_StateChanged;
        }

        /// <inheritdoc/>
        private void AttachedPeer_StateChanged(NetworkPeer peer, NetworkPeerState oldState)
        {
            if ((peer.State == NetworkPeerState.Failed) || (peer.State == NetworkPeerState.Disconnecting) || (peer.State == NetworkPeerState.Offline))
                this.peerConnector.RemovePeer(peer);
        }

        /// <inheritdoc/>
        public override object Clone()
        {
            return new PeerConnectorBehaviour(this.peerConnector);
        }
    }
}