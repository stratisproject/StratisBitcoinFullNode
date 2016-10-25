using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.FullNode.Consensus
{
	public class StopWatch
	{
		class StopwatchDisposable : IDisposable
		{
			public StopwatchDisposable(System.Diagnostics.Stopwatch watch, Action<long> act)
			{
				//Somehow, the watch is imprecise when time accumulate (maybe due to performance impact of calling underlying high precision API)
				//_Watch = watch;
				_Do = act;
				//watch.Restart();
				_Start = DateTimeOffset.UtcNow;
			}
			
			//System.Diagnostics.Stopwatch _Watch;
			Action<long> _Do;
			private readonly DateTimeOffset _Start;

			public void Dispose()
			{
				//_Watch.Stop();
				_Do((DateTimeOffset.UtcNow - _Start).Ticks);
			}
		}

		System.Diagnostics.Stopwatch _Watch = new System.Diagnostics.Stopwatch();
		public StopWatch()
		{

		}

		public IDisposable Start(Action<long> act)
		{
			return new StopwatchDisposable(_Watch, act);
		}
	}
}
