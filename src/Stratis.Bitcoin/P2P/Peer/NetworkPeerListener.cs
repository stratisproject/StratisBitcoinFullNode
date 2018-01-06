using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NetworkPeerListener : PollMessageListener<IncomingMessage>, IDisposable
    {
        public NetworkPeer Peer { get; private set; }
        private IDisposable subscription;
        private List<Func<IncomingMessage, bool>> predicates = new List<Func<IncomingMessage, bool>>();

        public NetworkPeerListener(NetworkPeer peer)
        {
            this.subscription = peer.MessageProducer.AddMessageListener(this);
            this.Peer = peer;
        }

        public NetworkPeerListener Where(Func<IncomingMessage, bool> predicate)
        {
            this.predicates.Add(predicate);
            return this;
        }

        public NetworkPeerListener OfType<TPayload>() where TPayload : Payload
        {
            this.predicates.Add(i => i.Message.Payload is TPayload);
            return this;
        }

        public TPayload ReceivePayload<TPayload>(CancellationToken cancellationToken = default(CancellationToken)) where TPayload : Payload
        {
            if (!this.Peer.IsConnected)
                throw new InvalidOperationException("The peer is not in a connected state");

            Queue<IncomingMessage> pushedAside = new Queue<IncomingMessage>();
            try
            {
                using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.Peer.Connection.CancellationSource.Token))
                {
                    while (true)
                    {
                        IncomingMessage message = this.ReceiveMessage(cancellation.Token);
                        if (this.predicates.All(p => p(message)))
                        {
                            if (message.Message.Payload is TPayload)
                                return (TPayload)message.Message.Payload;

                            pushedAside.Enqueue(message);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (this.Peer.Connection.CancellationSource.IsCancellationRequested)
                    throw new InvalidOperationException("The peer is not in a connected state");

                throw;
            }
            finally
            {
                while (pushedAside.Count != 0)
                    this.PushMessage(pushedAside.Dequeue());
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.subscription?.Dispose();
        }
    }
}