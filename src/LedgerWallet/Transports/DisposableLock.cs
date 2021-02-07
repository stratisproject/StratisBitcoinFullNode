using System;
using System.Threading;

namespace LedgerWallet.Transports
{
    public class DisposableLock
    {
        class ReleaseLockDisposable : IDisposable
        {
            readonly object l;
            public ReleaseLockDisposable(object l)
            {
                this.l = l;
                Monitor.Enter(l);
            }
            public void Dispose()
            {
                Monitor.Exit(l);
            }
        }

        readonly object l = new object();
        public IDisposable Lock()
        {
            return new ReleaseLockDisposable(l);
        }
    }
}
