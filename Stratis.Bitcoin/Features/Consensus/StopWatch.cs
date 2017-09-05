using System;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class StopWatch
    {
        class StopwatchDisposable : IDisposable
        {
            public StopwatchDisposable(System.Diagnostics.Stopwatch watch, Action<long> act)
            {
                //Somehow, the watch is imprecise when time accumulate (maybe due to performance impact of calling underlying high precision API)
                //_Watch = watch;
                this._Do = act;
                //watch.Restart();
                this._Start = DateTimeOffset.UtcNow;
            }

            //System.Diagnostics.Stopwatch _Watch;
            Action<long> _Do;
            private readonly DateTimeOffset _Start;

            public void Dispose()
            {
                //_Watch.Stop();
                this._Do((DateTimeOffset.UtcNow - this._Start).Ticks);
            }
        }

        public static StopWatch Instance = new StopWatch();
        System.Diagnostics.Stopwatch _Watch = new System.Diagnostics.Stopwatch();
        public StopWatch()
        {

        }

        public IDisposable Start(Action<long> act)
        {
            return new StopwatchDisposable(this._Watch, act);
        }
    }
}
