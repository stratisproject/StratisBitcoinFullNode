using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.P2P.Peer;

namespace Stratis.Bitcoin.P2P.Protocol
{
    /// <summary>
    /// Represents a callback routine to be called when a new message arrives to the listener.
    /// <para>
    /// It is guaranteed that only execution of the callback routine is executed at the time.
    /// </para>
    /// </summary>
    /// <param name="message">New message to be processed.</param>
    /// <param name="cancellationToken">Cancellation token that the callback method should use for its async operations to avoid blocking the listener during shutdown.</param>
    /// <typeparam name="T">Type of the messages that are being handled.</typeparam>
    public delegate Task ProcessMessageAsync<T>(T message, CancellationToken cancellationToken);

    /// <summary>
    /// Message listener that processes the incoming message using a user defined callback routine.
    /// </summary>
    /// <typeparam name="T">Type of the messages that are being handled.</typeparam>
    public class CallbackMessageListener<T> : IMessageListener<T>, IDisposable
    {
        /// <summary>Queue of the unprocessed incoming messages.</summary>
        private readonly IAsyncDelegateDequeuer<T> asyncQueue;

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="processMessageAsync">User defined callback routine to be executed when a new message arrives to the listener.</param>
        public CallbackMessageListener(IAsyncProvider asyncProvider, ProcessMessageAsync<T> processMessageAsync, INetworkPeer peer)
        {
            string queuerName = $"{nameof(CallbackMessageListener<T>)}-{typeof(T).Name}-{peer.PeerEndPoint?.ToString()}";
            this.asyncQueue = asyncProvider.CreateAndRunAsyncDelegateDequeuer<T>(queuerName, new Func<T, CancellationToken, Task>(processMessageAsync));
        }

        /// <inheritdoc/>
        /// <remarks>Adds the newly received message to the queue.</remarks>
        public void PushMessage(T message)
        {
            this.asyncQueue.Enqueue(message);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.asyncQueue.Dispose();
        }
    }
}