using Stratis.Bitcoin.Logging;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
    public interface IPeriodicTask
    {
        string Name { get; }

        void RunOnce();
        PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false);
    }

    public class PeriodicTask : IPeriodicTask
    {
        public PeriodicTask(string name, Action<CancellationToken> loop)
        {
            _Name = name;
            this._Loop = loop;
        }

        Action<CancellationToken> _Loop;

        private readonly string _Name;
        public string Name
        {
            get
            {
                return _Name;
            }
        }

        public PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false)
        {
            var t = new Thread(() =>
            {
                Exception uncatchException = null;
                Logs.FullNode.LogInformation(_Name + " starting");
                try
                {
                    if (delayStart)
                        cancellation.WaitHandle.WaitOne(refreshRate);

                    while (!cancellation.IsCancellationRequested)
                    {
                        _Loop(cancellation);
                        cancellation.WaitHandle.WaitOne(refreshRate);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    if (!cancellation.IsCancellationRequested)
                        uncatchException = ex;
                }
                catch (Exception ex)
                {
                    uncatchException = ex;
                }
                finally
                {
                    Logs.FullNode.LogInformation(Name + " stopping");
                }

                if (uncatchException != null)
                {
                    Logs.FullNode.LogCritical(new EventId(0), uncatchException, _Name + " threw an unhandled exception");
                }
            });
            t.IsBackground = true;
            t.Name = _Name;
            t.Start();            
            return this;
        }

        public void RunOnce()
        {
            _Loop(CancellationToken.None);
        }
    }
}
