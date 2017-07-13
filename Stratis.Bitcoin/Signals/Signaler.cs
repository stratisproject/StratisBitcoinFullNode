using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Signals
{
    public interface IBroadcast<in T>
    {
        void Broadcast(T item);
    }

    public interface ISignaler<T> : IBroadcast<T>, IObservable<T>
    {
    }

    public class Signaler<T> : ISignaler<T>
    {
        private readonly ISubject<T> subject;
        private readonly IObservable<T> observable;

        public Signaler() : this(new Subject<T>())
        {            
        }

        public Signaler(ISubject<T> subject)
        {
            Guard.NotNull(subject, nameof(subject));

            this.subject = subject;
            this.subject = Subject.Synchronize(this.subject);            
            this.observable = this.subject.AsObservable();
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            Guard.NotNull(observer, nameof(observer));

            return this.observable.Subscribe(observer);
        }

        public void Broadcast(T item)
        {
            Guard.NotNull(item, nameof(item));

            this.subject.OnNext(item);
        }
    }
}