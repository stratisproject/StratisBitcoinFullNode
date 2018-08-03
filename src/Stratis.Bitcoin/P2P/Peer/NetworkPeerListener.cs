using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.P2P.Protocol;
using Stratis.Bitcoin.P2P.Protocol.Payloads;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Peer
{
    /// <summary>
    /// Message listener that waits until a specific payload is received and returns it to the caller.
    /// </summary>
    public class NetworkPeerListener : IMessageListener<IncomingMessage>, IDisposable
    {
        /// <summary>Queue of unprocessed messages.</summary>
        private readonly AsyncQueue<IncomingMessage> asyncQueue;

        /// <summary>Connected network peer that we receive messages from.</summary>
        private readonly INetworkPeer peer;

        /// <summary>Registration to the message producer of the connected peer.</summary>
        private readonly MessageProducerRegistration<IncomingMessage> messageProducerRegistration;

        /// <summary>
        /// Initializes the instance of the object and subscribes to the peer's message producer.
        /// </summary>
        /// <param name="peer">Connected network peer that we receive messages from.</param>
        public NetworkPeerListener(INetworkPeer peer)
        {
            this.asyncQueue = new AsyncQueue<IncomingMessage>();
            this.messageProducerRegistration = peer.MessageProducer.AddMessageListener(this);
            this.peer = peer;
        }

        /// <inheritdoc/>
        /// <remarks>Adds the newly received message to the queue.</remarks>
        public void PushMessage(IncomingMessage message)
        {
            this.asyncQueue.Enqueue(message);
        }

        /// <summary>
        /// Waits until a message with a specific payload arrives from the peer.
        /// </summary>
        /// <typeparam name="TPayload">Type of payload to wait for.</typeparam>
        /// <param name="cancellationToken">Cancellation token to abort the waiting operation.</param>
        /// <returns>Payload of the specific type received from the peer.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the peer is not connected when the method is called, or when <see cref="Dispose"/>
        /// has been called while we are waiting for the message.</exception>
        public async Task<TPayload> ReceivePayloadAsync<TPayload>(CancellationToken cancellationToken = default(CancellationToken)) where TPayload : Payload
        {
            if (!this.peer.IsConnected)
                throw new OperationCanceledException("The peer is not in a connected state");

            using (CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.peer.Connection.CancellationSource.Token))
            {
                while (true)
                {
                    IncomingMessage message = await this.asyncQueue.DequeueAsync(cancellation.Token).ConfigureAwait(false);
                    if (message.Message.Payload is TPayload payload)
                        return payload;
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.messageProducerRegistration.Dispose();
            this.asyncQueue.Dispose();
        }
    }
}