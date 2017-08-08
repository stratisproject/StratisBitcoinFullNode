using System;
using System.Reactive;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Signals
{
    /// <summary>
    /// Consumer of messages produced by <see cref="Signaler{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of messages being consumed.</typeparam>
    public interface ISignalObserver<T> : IObserver<T>, IDisposable
    {
    }

    /// <inheritdoc />
    public abstract class SignalObserver<T> : ObserverBase<T>, ISignalObserver<T>
    {
        /// <inheritdoc />
        protected override void OnErrorCore(Exception error)
        {
            Guard.NotNull(error, nameof(error));
            // Nothing to do.
        }

        /// <inheritdoc />
        protected override void OnCompletedCore()
        {
            // Nothing to do.
        }
    }
}