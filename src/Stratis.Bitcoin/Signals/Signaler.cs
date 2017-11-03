using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Signals
{
    /// <summary>
    /// Defines provider with ability to broadcast messages to all its subscribers.
    /// </summary>
    /// <typeparam name="T">Type of messages being sent.</typeparam>
    public interface IBroadcast<T>
    {
        /// <summary>
        /// Sends a message to all subscribers.
        /// </summary>
        /// <param name="item">Message to send, must not be <c>null</c>.</param>
        void Broadcast(T item);
    }

    /// <summary>
    /// Provider that allows distribution of messages to multiple subscribers.
    /// </summary>
    /// <typeparam name="T">Type of messages to be sent by the signaler.</typeparam>
    public interface ISignaler<T> : IBroadcast<T>, IObservable<T>
    {
    }

    /// <inheritdoc />
    /// <remarks>This is synchronous implementation of the provider.</remarks>
    public class Signaler<T> : ISignaler<T>
    {
        /// <summary>Subject to distribute signaler's messages</summary>
        private readonly ISubject<T> subject;

        /// <summary>Observable part of <see cref="subject"/> that observers can subscribe to.</summary>
        private readonly IObservable<T> observable;

        /// <summary>
        /// Initializes a new signaler with newly created subject.
        /// </summary>
        public Signaler() : this(new Subject<T>())
        {            
        }

        /// <summary>
        /// Initializes a new signaler with a given subject.
        /// </summary>
        /// <param name="subject">Subject to be used to broadcast messages to subscribers.</param>
        public Signaler(ISubject<T> subject)
        {
            Guard.NotNull(subject, nameof(subject));

            this.subject = subject;
            this.subject = Subject.Synchronize(this.subject);            
            this.observable = this.subject.AsObservable();
        }

        /// <inheritdoc />
        public IDisposable Subscribe(IObserver<T> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.observable.Subscribe(observer);
        }

        /// <inheritdoc />
        public void Broadcast(T item)
        {
            this.subject.OnNext(item);
        }
    }
}