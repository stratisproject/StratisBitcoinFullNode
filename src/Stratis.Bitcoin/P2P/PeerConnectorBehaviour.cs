using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Maintain connection to a given set of peers.</summary>
    internal sealed class PeerConnectorBehaviour : NodeBehavior
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
            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
        }

        /// <inheritdoc/>
        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
        }

        /// <inheritdoc/>
        private void AttachedNode_StateChanged(NetworkPeer node, NetworkPeerState oldState)
        {
            if (node.State == NetworkPeerState.HandShaked)
                this.peerConnector.AddNode(node);

            if ((node.State == NetworkPeerState.Failed) || (node.State == NetworkPeerState.Disconnecting) || (node.State == NetworkPeerState.Offline))
                this.peerConnector.RemoveNode(node);
        }

        /// <inheritdoc/>
        public override object Clone()
        {
            return new PeerConnectorBehaviour(this.peerConnector);
        }
    }
}