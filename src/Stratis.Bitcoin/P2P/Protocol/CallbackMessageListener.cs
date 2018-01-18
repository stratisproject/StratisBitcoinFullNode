using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Protocol
{
    /// <summary>
    /// Represents a callback rountine to be called when a new message arrives to the listener.
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
        /// <summary>User defined callback routine to be executed when a new message arrives to the listener.</summary>
        private readonly ProcessMessageAsync<T> processMessageAsync;

        /// <summary>Queue of the unprocessed incoming messages.</summary>
        private readonly AsyncQueue<T> asyncQueue;

        /// <summary>
        /// Initializes the instance of the object.
        /// </summary>
        /// <param name="processMessageAsync">User defined callback routine to be executed when a new message arrives to the listener.</param>
        public CallbackMessageListener(ProcessMessageAsync<T> processMessageAsync)
        {
            this.asyncQueue = new AsyncQueue<T>(new AsyncQueue<T>.OnEnqueueAsync(processMessageAsync));
            this.processMessageAsync = processMessageAsync;
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