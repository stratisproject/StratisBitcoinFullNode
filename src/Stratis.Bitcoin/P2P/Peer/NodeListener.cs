using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NBitcoin.Protocol;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;

namespace Stratis.Bitcoin.P2P.Peer
{
    public class NodeListener : PollMessageListener<IncomingMessage>, IDisposable
    {
        public Node Node { get; private set; }
        private IDisposable subscription;
        private List<Func<IncomingMessage, bool>> predicates = new List<Func<IncomingMessage, bool>>();

        public NodeListener(Node node)
        {
            this.subscription = node.MessageProducer.AddMessageListener(this);
            this.Node = node;
        }

        public NodeListener Where(Func<IncomingMessage, bool> predicate)
        {
            this.predicates.Add(predicate);
            return this;
        }

        public NodeListener OfType<TPayload>() where TPayload : Payload
        {
            this.predicates.Add(i => i.Message.Payload is TPayload);
            return this;
        }

        public TPayload ReceivePayload<TPayload>(CancellationToken cancellationToken = default(CancellationToken)) where TPayload : Payload
        {
            if (!this.Node.IsConnected)
                throw new InvalidOperationException("The node is not in a connected state");

            Queue<IncomingMessage> pushedAside = new Queue<IncomingMessage>();
            try
            {
                while (true)
                {
                    IncomingMessage message = ReceiveMessage(CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.Node.connection.Cancel.Token).Token);
                    if (this.predicates.All(p => p(message)))
                    {
                        if (message.Message.Payload is TPayload)
                            return (TPayload)message.Message.Payload;

                        pushedAside.Enqueue(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (this.Node.connection.Cancel.IsCancellationRequested)
                    throw new InvalidOperationException("The node is not in a connected state");

                throw;
            }
            finally
            {
                while (pushedAside.Count != 0)
                    this.PushMessage(pushedAside.Dequeue());
            }
        }

        public void Dispose()
        {
            this.subscription?.Dispose();
        }
    }
}