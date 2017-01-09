using Stratis.Bitcoin.Logging;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
	public class PeriodicTask
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

		public PeriodicTask Start(CancellationToken cancellation)
		{
			var t = new Thread(() =>
			{
				Exception uncatchException = null;
				Logs.FullNode.LogInformation(_Name + " starting");
				try
				{
					while(true)
					{
						cancellation.WaitHandle.WaitOne(TimeSpan.FromMinutes(5.0));
						cancellation.ThrowIfCancellationRequested();
						_Loop(cancellation);
					}
				}
				catch(OperationCanceledException ex)
				{
					if(!cancellation.IsCancellationRequested)
						uncatchException = ex;
				}
				catch(Exception ex)
				{
					uncatchException = ex;
				}
				if(uncatchException != null)
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
