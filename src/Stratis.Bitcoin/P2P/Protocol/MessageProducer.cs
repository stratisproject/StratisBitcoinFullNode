using System;
using ConcurrentCollections;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.P2P.Protocol
{
    /// <summary>
    /// Binding between <see cref="MessageProducer{T}"/> and <see cref="IMessageListener{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of the messages that are being handled.</typeparam>
    public class MessageProducerRegistration<T> : IDisposable
    {
        /// <summary>Producer of messages.</summary>
        private readonly MessageProducer<T> producer;

        /// <summary>Consumer of messages.</summary>
        private readonly IMessageListener<T> listener;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        /// <param name="listener">Consumer of messages.</param>
        /// <param name="producer">Producer of messages.</param>
        public MessageProducerRegistration(IMessageListener<T> listener, MessageProducer<T> producer)
        {
            this.listener = listener;
            this.producer = producer;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.producer.RemoveMessageListener(this.listener);
        }
    }

    /// <summary>
    /// Distributor of messages to attached consumers.
    /// </summary>
    /// <typeparam name="T">Type of the messages that are being handled.</typeparam>
    public class MessageProducer<T>
    {
        /// <summary>List of attached consumers of this producer's messages.</summary>
        private readonly ConcurrentHashSet<IMessageListener<T>> listeners;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public MessageProducer()
        {
            this.listeners = new ConcurrentHashSet<IMessageListener<T>>();
        }

        /// <summary>
        /// Add a new consumer to distribute messages to.
        /// </summary>
        /// <param name="listener">New consumer to distribute messages to.</param>
        /// <returns>Disposable binding between the producer and the consumer.</returns>
        public MessageProducerRegistration<T> AddMessageListener(IMessageListener<T> listener)
        {
            Guard.NotNull(listener, nameof(listener));

            this.listeners.Add(listener);

            var registration = new MessageProducerRegistration<T>(listener, this);
            return registration;
        }

        /// <summary>
        /// Stops distribution of message to a registered consumer.
        /// </summary>
        /// <param name="listener">Registered consumer to stop distributing messages to.</param>
        public void RemoveMessageListener(IMessageListener<T> listener)
        {
            Guard.NotNull(listener, nameof(listener));

            this.listeners.TryRemove(listener);
        }

        /// <summary>
        /// Distributes a message among all attached consumers.
        /// </summary>
        /// <param name="message">Message to distribute.</param>
        public void PushMessage(T message)
        {
            Guard.NotNull(message, nameof(message));

            foreach (IMessageListener<T> listener in this.listeners)
            {
                listener.PushMessage(message);
            }
        }
    }
}