using System;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using TracerAttributes;

namespace Stratis.Bitcoin.P2P.Protocol.Behaviors
{
    public interface INetworkPeerBehavior : IDisposable
    {
        INetworkPeer AttachedPeer { get; }

        void Attach(INetworkPeer peer);

        void Detach();

        INetworkPeerBehavior Clone();
    }

    public abstract class NetworkPeerBehavior : INetworkPeerBehavior
    {
        private object cs = new object();

        public INetworkPeer AttachedPeer { get; private set; }

        protected abstract void AttachCore();

        protected abstract void DetachCore();

        public abstract object Clone();

        [NoTrace]
        public void Attach(INetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            if (this.AttachedPeer != null)
                throw new InvalidOperationException("Behavior already attached to a peer");

            lock (this.cs)
            {
                if (Disconnected(peer))
                    return;

                this.AttachedPeer = peer;

                this.AttachCore();
            }
        }

        [NoTrace]
        protected void AssertNotAttached()
        {
            if (this.AttachedPeer != null)
                throw new InvalidOperationException("Can't modify the behavior while it is attached");
        }

        private static bool Disconnected(INetworkPeer peer)
        {
            return (peer.State == NetworkPeerState.Created) || (peer.State == NetworkPeerState.Disconnecting) ||
                   (peer.State == NetworkPeerState.Failed) || (peer.State == NetworkPeerState.Offline);
        }

        [NoTrace]
        public void Detach()
        {
            lock (this.cs)
            {
                if (this.AttachedPeer == null)
                    return;

                this.DetachCore();
            }
        }

        /// <inheritdoc />
        [NoTrace]
        public virtual void Dispose()
        {
            this.AttachedPeer = null;
        }

        [NoTrace]
        INetworkPeerBehavior INetworkPeerBehavior.Clone()
        {
            return (INetworkPeerBehavior)this.Clone();
        }
    }
}