using System;

namespace Stratis.Bitcoin.Base.BackgroundWork
{
    public interface IAsyncDelegate : IDisposable { }

    public interface IAsyncDelegate<T> : IAsyncDelegate { }
}
