using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Utilities
{
    public interface IPeriodicTask
    {
        string Name { get; }

        void RunOnce();
        PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false);
    }

    public class PeriodicTask : IPeriodicTask
    {
        public PeriodicTask(string name, ILogger logger, Action<CancellationToken> loop)
        {
            this._Name = name;
            this.logger = logger;
            this._Loop = loop;
        }

        Action<CancellationToken> _Loop;

        private readonly string _Name;
        private readonly ILogger logger;

        public string Name
        {
            get
            {
                return this._Name;
            }
        }

        public PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false)
        {
            var t = new Thread(() =>
            {
                Exception uncatchException = null;
                this.logger.LogInformation(this._Name + " starting");
                try
                {
                    if (delayStart)
                        cancellation.WaitHandle.WaitOne(refreshRate);

                    while (!cancellation.IsCancellationRequested)
                    {
                        this._Loop(cancellation);
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
                    this.logger.LogInformation(this.Name + " stopping");
                }

                if (uncatchException != null)
                {
                    this.logger.LogCritical(new EventId(0), uncatchException, this._Name + " threw an unhandled exception");
                }
            });
            t.IsBackground = true;
            t.Name = this._Name;
            t.Start();            
            return this;
        }

        public void RunOnce()
        {
            this._Loop(CancellationToken.None);
        }
    }
}
