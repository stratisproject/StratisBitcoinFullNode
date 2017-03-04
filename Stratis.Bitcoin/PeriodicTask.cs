using Stratis.Bitcoin.Logging;
using System;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
	public class PeriodicAsyncTask
	{
		public PeriodicAsyncTask(string name, Func<CancellationToken, Task> loop)
		{
			_Name = name;
			this.loopAsync = loop;
		}

		readonly Func<CancellationToken, Task> loopAsync;

		private readonly string _Name;
		public string Name
		{
			get
			{
				return _Name;
			}
		}

		public PeriodicAsyncTask StartAsync(CancellationToken cancellation, TimeSpan refreshRate, TimeSpan? delayStart = null)
		{
			Task.Run(async () =>
			{
				Exception uncatchException = null;
				Logs.FullNode.LogInformation(_Name + " starting");
				try
				{
					if (delayStart != null)
						await Task.Delay(delayStart.Value, cancellation).ConfigureAwait(false);

					while (!cancellation.IsCancellationRequested)
					{
						await loopAsync(cancellation).ConfigureAwait(false);
						await Task.Delay(refreshRate, cancellation).ConfigureAwait(false);
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
				if (uncatchException != null)
				{
					Logs.FullNode.LogCritical(new EventId(0), uncatchException, _Name + " threw an unhandled exception");
				}
			}, cancellation);

			return this;
		}

		public Task RunOnce()
		{
			return loopAsync(CancellationToken.None);
		}
	}

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

		public PeriodicTask Start(CancellationToken cancellation, TimeSpan refreshRate, bool delayStart = false)
		{
			var t = new Thread(() =>
			{
				Exception uncatchException = null;
				Logs.FullNode.LogInformation(_Name + " starting");
				try
				{
					if (delayStart)
						cancellation.WaitHandle.WaitOne(refreshRate);//TimeSpan.FromMinutes(5.0));

					while (!cancellation.IsCancellationRequested)
					{
						_Loop(cancellation);
						cancellation.WaitHandle.WaitOne(refreshRate);//TimeSpan.FromMinutes(5.0));
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
