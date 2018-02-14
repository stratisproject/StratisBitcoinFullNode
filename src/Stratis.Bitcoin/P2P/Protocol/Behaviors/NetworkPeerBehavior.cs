using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Protocol.Behaviors
{
    public interface INetworkPeerBehavior
    {
        INetworkPeer AttachedPeer { get; }
        void Attach(INetworkPeer peer);
        void Detach();
        INetworkPeerBehavior Clone();
    }

    public abstract class NetworkPeerBehavior : INetworkPeerBehavior
    {
        public INetworkPeer AttachedPeer { get; private set; }

        public abstract object Clone();

        public delegate Task OnPayloadReceived<T>(INetworkPeer peer, T payload, long lenght);
        public delegate Task OnPayloadReceivedCompact<T>(INetworkPeer peer, T payload);

        protected Dictionary<Type, OnPayloadReceived<object>> subscriptions;

        private readonly object lockObject;
        private List<IDisposable> disposables;

        public NetworkPeerBehavior()
        {
            this.lockObject = new object();
            this.disposables = new List<IDisposable>();
            this.subscriptions = new Dictionary<Type, OnPayloadReceived<object>>();
        }

        protected void RegisterDisposable(IDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        public void Attach(INetworkPeer peer)
        {
            Guard.NotNull(peer, nameof(peer));

            if (this.AttachedPeer != null)
                throw new InvalidOperationException("Behavior already attached to a peer");

            lock (this.lockObject)
            {
                this.AttachedPeer = peer;
                if (Disconnected(peer))
                    return;

                this.AttachedPeer.MessageReceived.Register(this.OnMessageReceivedAsync);
                this.AttachedPeer.StateChanged.Register(this.OnStateChangedAsync);

                this.AttachCore();
            }
        }

        private async Task OnMessageReceivedAsync(INetworkPeer peer, IncomingMessage message)
        {
            Type payloadType = message.Message.Payload.GetType();

            foreach (var subscription in this.subscriptions.Where(x => x.Key == payloadType))
                await subscription.Value(peer, message.Message.Payload, message.Length).ConfigureAwait(false);
        }

        protected void SubscribeToPayload<T>(OnPayloadReceivedCompact<T> callback) where T : Payload
        {
            this.subscriptions.Add(typeof(T), (peer, payload, lenght) => callback(peer, payload as T));
        }

        protected void SubscribeToPayload<T>(OnPayloadReceived<T> callback) where T : Payload
        {
            this.subscriptions.Add(typeof(T), (peer, payload, lenght) => callback(peer, payload as T, lenght));
        }

        protected virtual async Task OnStateChangedAsync(INetworkPeer peer, NetworkPeerState oldState)
        {
        }

        protected virtual void AttachCore()
        {
        }

        protected virtual void DetachCore()
        {
        }

        /// <summary>
        /// Makes a wrapper around <see cref="OnPayloadReceivedCompact"/> callback that ignores <see cref="OperationCanceledException"/> exception but throws others. 
        /// </summary>
        /// <remarks>If any exception occurs it will be logged.</remarks>
        protected async Task ProcessPayloadAndHandleErrors<T>(T payload, INetworkPeer peer, ILogger logger, OnPayloadReceivedCompact<T> callback) where T : Payload
        {
            logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(peer), peer.RemoteSocketEndpoint, nameof(payload), payload);

            try
            {
                await callback(peer, payload).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                logger.LogTrace("(-)[CANCELED_EXCEPTION]");
                return;
            }
            catch (Exception ex)
            {
                logger.LogError("Exception occurred: {0}", ex.ToString());
                throw;
            }

            logger.LogTrace("()");
        }

        protected void AssertNotAttached()
        {
            if (this.AttachedPeer != null)
                throw new InvalidOperationException("Can't modify the behavior while it is attached");
        }

        private static bool Disconnected(INetworkPeer peer)
        {
            return (peer.State == NetworkPeerState.Disconnecting) || (peer.State == NetworkPeerState.Failed) || (peer.State == NetworkPeerState.Offline);
        }

        public void Detach()
        {
            lock (this.lockObject)
            {
                if (this.AttachedPeer == null)
                    return;

                this.AttachedPeer.MessageReceived.Unregister(this.OnMessageReceivedAsync);
                this.AttachedPeer.StateChanged.Unregister(this.OnStateChangedAsync);
                this.subscriptions.Clear();

                this.DetachCore();
                foreach (IDisposable dispo in this.disposables)
                    dispo.Dispose();

                this.disposables.Clear();
                this.AttachedPeer = null;
            }
        }

        INetworkPeerBehavior INetworkPeerBehavior.Clone()
        {
            return (INetworkPeerBehavior)this.Clone();
        }
    }
}