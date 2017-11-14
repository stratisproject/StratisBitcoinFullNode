using System;
using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;

namespace Stratis.Bitcoin.P2P
{
    /// <summary>
    /// Behaviour implementation that encapsulates <see cref="PeerAddressManager"/>.
    /// <para>
    /// Subscribes to state change events from <see cref="Node"/> and relays connection and handshake attempts to 
    /// the <see cref="PeerAddressManager"/> instance.
    /// </para>
    /// <para>
    /// Peer discovery also get initiated from here.
    /// </para>
    /// </summary>
    public sealed class PeerAddressManagerBehaviour : NodeBehavior
    {
        public PeerAddressManagerBehaviour(PeerAddressManager manager)
        {
            this.addressManager = manager ?? throw new ArgumentNullException("manager");
            this.Mode = PeerAddressManagerBehaviourMode.AdvertiseDiscover;
        }

        int peersToDiscover = 1000;
        public int PeersToDiscover
        {
            get { return this.peersToDiscover; }
            set { this.peersToDiscover = value; }
        }

        public PeerAddressManagerBehaviourMode Mode { get; set; }

        private PeerAddressManager addressManager;
        public PeerAddressManager AddressManager
        {
            get { return this.addressManager; }
            set
            {
                AssertNotAttached();

                this.addressManager = value ?? throw new ArgumentNullException("value");
            }
        }

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
        }

        void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Advertise) != 0)
            {
                if (message.Message.Payload is GetAddrPayload getaddr)
                    node.SendMessageAsync(new AddrPayload(this.AddressManager.SelectPeersToConnectTo().Take(1000).ToArray()));
            }

            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                if (message.Message.Payload is AddrPayload addr)
                    this.AddressManager.AddPeer(addr.Addresses, node.RemoteSocketAddress);
            }
        }

        void AttachedNode_StateChanged(Node node, NodeState previousState)
        {
            if ((this.Mode & PeerAddressManagerBehaviourMode.Discover) != 0)
            {
                //TODO: We need to refactor this as the StateChanged event handlers only gets attached
                //AFTER the node has connected, which means that we can never go:
                //if (node.State == NodeState.Connected)
                //which is more intuitive.
                //This happens in PeerDiscovery as well where we connect and then disconnect straight after.
                if (node.State <= NodeState.Disconnecting && previousState == NodeState.HandShaked)
                    this.AddressManager.PeerConnected(node.Peer.Endpoint, DateTimeOffset.Now);

                if (node.State == NodeState.HandShaked)
                    this.AddressManager.PeerHandshaked(node.Peer.Endpoint, DateTimeOffset.Now);
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
        }

        public void DiscoverPeers(Network network, NodeConnectionParameters parameters)
        {
            if (this.Mode.HasFlag(PeerAddressManagerBehaviourMode.Discover))
                this.AddressManager.DiscoverPeers(network, parameters, this.PeersToDiscover);
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new PeerAddressManagerBehaviour(this.AddressManager)
            {
                PeersToDiscover = this.PeersToDiscover,
                Mode = this.Mode
            };
        }

        #endregion
    }

    [Flags]
    public enum PeerAddressManagerBehaviourMode
    {
        /// <summary>Do not advertise nor discover new peers.</summary>
        None = 0,

        /// <summary>Only advertise known peers.</summary>
        Advertise = 1,

        /// <summary>Only discover peers.</summary>
        Discover = 2,

        /// <summary>Advertise known peer and discover peer.</summary>
        AdvertiseDiscover = 3,
    }
}