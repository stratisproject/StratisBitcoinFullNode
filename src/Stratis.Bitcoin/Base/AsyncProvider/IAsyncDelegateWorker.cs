using System;

namespace Stratis.Bitcoin.Base.AsyncProvider
{
    public interface IAsyncDelegateWorker : IDisposable { }

    /// <summary>
    /// Define a delegate that is called asynchronously in the background whenever a new <typeparamref name="T"/> is queued and run it.
    /// </summary>
    /// <typeparam name="T">Type of the queued items used in the delegate.</typeparam>
    /// <seealso cref="System.IDisposable" />
    public interface IAsyncDelegateWorker<T> : IAsyncDelegateWorker { }
}
