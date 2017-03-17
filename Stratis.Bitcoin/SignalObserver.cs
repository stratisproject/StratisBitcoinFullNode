using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Logging;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin
{
    public interface ISignalObserver<T> : IObserver<T>, IDisposable
    {
    }

    public abstract class SignalObserver<T> : ObserverBase<T>, ISignalObserver<T>
    {
        protected override void OnErrorCore(Exception error)
        {
            Guard.NotNull(error, nameof(error));

            Logging.Logs.FullNode.LogError(error.ToString());
        }

        protected override void OnCompletedCore()
        {
            // nothing to do
        }
    }
}