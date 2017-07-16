using System;
using System.Reactive;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Signals
{
    public interface ISignalObserver<T> : IObserver<T>, IDisposable
    {
    }

    public abstract class SignalObserver<T> : ObserverBase<T>, ISignalObserver<T>
    {
        protected override void OnErrorCore(Exception error)
        {
            Guard.NotNull(error, nameof(error));
        }

        protected override void OnCompletedCore()
        {
            // nothing to do
        }
    }
}