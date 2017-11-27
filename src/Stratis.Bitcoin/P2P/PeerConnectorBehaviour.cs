using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>Maintain connection to a given set of peers.</summary>
    internal sealed class PeerConnectorBehaviour : NodeBehavior
    {
        /// <summary>The peer connector this behaviour relates to.</summary>
        private readonly PeerConnector peerConnector;

        internal PeerConnectorBehaviour(PeerConnector peerConnector)
        {
            this.peerConnector = peerConnector;
        }

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += this.AttachedNode_StateChanged;
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
        }

        private void AttachedNode_StateChanged(NetworkPeer node, NetworkPeerState oldState)
        {
            if (node.State == NetworkPeerState.HandShaked)
                this.peerConnector.AddNode(node);

            if ((node.State == NetworkPeerState.Failed) || (node.State == NetworkPeerState.Disconnecting) || (node.State == NetworkPeerState.Offline))
                this.peerConnector.RemoveNode(node);
        }

        public override object Clone()
        {
            return new PeerConnectorBehaviour(this.peerConnector);
        }
    }
}
