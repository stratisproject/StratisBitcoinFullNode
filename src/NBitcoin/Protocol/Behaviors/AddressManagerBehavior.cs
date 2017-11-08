using System;
using System.Linq;

namespace NBitcoin.Protocol.Behaviors
{
    [Flags]
    public enum AddressManagerBehaviorMode
    {
        /// <summary>Do not advertize nor discover new peers.</summary>
        None = 0,

        /// <summary>Only advertize known peers.</summary>
        Advertize = 1,

        /// <summary>Only discover peers.</summary>
        Discover = 2,

        /// <summary>Advertize known peer and discover peer.</summary>
        AdvertizeDiscover = 3,
    }

    /// <summary>
    /// The AddressManagerBehavior class will respond to getaddr and register advertised nodes from addr messages to the AddressManager.
    /// The AddressManagerBehavior will also receive feedback about connection attempt and success of discovered peers to the AddressManager, so it can be used later to find valid peer faster.
    /// </summary>
    public class AddressManagerBehavior : NodeBehavior
    {
        /// <summary>
        /// The minimum number of peers to discover before trying to connect to a node using the AddressManager (Default: 1000).
        /// </summary>
        public int PeersToDiscover { get; set; }

        public AddressManagerBehaviorMode Mode { get; set; }

        private AddressManager addressManager;
        public AddressManager AddressManager
        {
            get
            {
                return this.addressManager;
            }
            set
            {
                this.AssertNotAttached();
                this.addressManager = value ?? throw new ArgumentNullException("value");
            }
        }

        public AddressManagerBehavior()
        {
            this.PeersToDiscover = 1000;
        }

        public static AddressManager GetAddrman(Node node)
        {
            return GetAddrman(node.Behaviors);
        }

        public static AddressManager GetAddrman(NodeConnectionParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            return GetAddrman(parameters.TemplateBehaviors);
        }

        public static AddressManager GetAddrman(NodeBehaviorsCollection behaviors)
        {
            if (behaviors == null)
                throw new ArgumentNullException("behaviors");

            AddressManagerBehavior behavior = behaviors.Find<AddressManagerBehavior>();
            if (behavior == null)
                return null;

            return behavior.AddressManager;
        }

        public static void SetAddrman(Node node, AddressManager addrman)
        {
            if (node == null)
                throw new ArgumentNullException("node");

            SetAddrman(node.Behaviors, addrman);
        }

        public static void SetAddrman(NodeConnectionParameters parameters, AddressManager addrman)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            SetAddrman(parameters.TemplateBehaviors, addrman);
        }

        public static void SetAddrman(NodeBehaviorsCollection behaviors, AddressManager addrman)
        {
            if (behaviors == null)
                throw new ArgumentNullException("behaviors");

            AddressManagerBehavior behavior = behaviors.Find<AddressManagerBehavior>();
            if (behavior == null)
            {
                // FIXME: Please take a look at this.
                behavior = new AddressManagerBehavior(addrman);
                behaviors.Add(behavior);
            }

            behavior.AddressManager = addrman;
        }

        public AddressManagerBehavior(AddressManager manager)
        {
            this.addressManager = manager ?? throw new ArgumentNullException("manager");
            this.Mode = AddressManagerBehaviorMode.AdvertizeDiscover;
        }

        protected override void AttachCore()
        {
            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
        }

        void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if ((this.Mode & AddressManagerBehaviorMode.Advertize) != 0)
            {
                if (message.Message.Payload is GetAddrPayload getaddr)
                {
                    node.SendMessageAsync(new AddrPayload(this.AddressManager.GetAddr().Take(1000).ToArray()));
                }
            }

            if ((this.Mode & AddressManagerBehaviorMode.Discover) != 0)
            {
                if (message.Message.Payload is AddrPayload addr)
                {
                    this.AddressManager.Add(addr.Addresses, node.RemoteSocketAddress);
                }
            }
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
            if ((this.Mode & AddressManagerBehaviorMode.Discover) != 0)
            {
                if (node.State <= NodeState.Disconnecting && oldState == NodeState.HandShaked)
                    this.AddressManager.Connected(node.Peer);

                if (node.State == NodeState.HandShaked)
                    this.AddressManager.Good(node.Peer);
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= AttachedNode_StateChanged;
        }

        #region ICloneable Members

        public override object Clone()
        {
            return new AddressManagerBehavior(this.AddressManager)
            {
                PeersToDiscover = this.PeersToDiscover,
                Mode = this.Mode
            };
        }

        internal void DiscoverPeers(Network network, NodeConnectionParameters parameters)
        {
            if (this.Mode.HasFlag(AddressManagerBehaviorMode.Discover))
                this.AddressManager.DiscoverPeers(network, parameters, this.PeersToDiscover);
        }

        #endregion
    }
}