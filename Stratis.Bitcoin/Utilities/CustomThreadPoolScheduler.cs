using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
	public class CustomThreadPoolTaskScheduler : TaskScheduler, IDisposable
	{
		int _ThreadCount;
		public CustomThreadPoolTaskScheduler(int threadCount, int maxQueued, string name = null)
		{
			this._ThreadCount = threadCount;
			this._Tasks = new BlockingCollection<Task>(new ConcurrentQueue<Task>(), maxQueued);
            this._AvailableThreads = threadCount;
			for(int i = 0; i < threadCount; i++)
			{
				new Thread(Do)
				{
					IsBackground = true,
					Name = name
				}.Start();
			}
		}

		public override int MaximumConcurrencyLevel
		{
			get
			{
				return this._ThreadCount;
			}
		}

		CancellationTokenSource _Cancel = new CancellationTokenSource();
		void Do(object state)
		{
			try
			{
				foreach(var task in this._Tasks.GetConsumingEnumerable(this._Cancel.Token))
				{
					Interlocked.Decrement(ref this._AvailableThreads);
					TryExecuteTask(task);
					Interlocked.Increment(ref this._AvailableThreads);
					if(this.RemainingTasks == 0)
                        this._Finished.Set();
				}
			}
			catch(OperationCanceledException)
			{
			}
		}

		public int QueuedCount
		{
			get
			{
				return this._Tasks.Count;
			}
		}

		int _AvailableThreads;
		public int AvailableThreads
		{
			get
			{
				return this._AvailableThreads;
			}
		}

		public int RemainingTasks
		{
			get
			{
				return (this._ThreadCount - this.AvailableThreads) + this.QueuedCount;
			}
		}

		public int ThreadsCount
		{
			get
			{
				return this._ThreadCount;
			}
		}

		BlockingCollection<Task> _Tasks;
		protected override IEnumerable<Task> GetScheduledTasks()
		{
			return this._Tasks;
		}

		protected override void QueueTask(Task task)
		{
			AssertNotDisposed();
            this._Tasks.Add(task);
		}

		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
		{
			AssertNotDisposed();
			return false;
		}

		#region IDisposable Members

		bool _disposed;
		public void Dispose()
		{
            this._disposed = true;
            this._Cancel.Cancel();
		}

		#endregion

		AutoResetEvent _Finished = new AutoResetEvent(false);
		public void WaitFinished()
		{
			AssertNotDisposed();
			while(true)
			{
				if(this._disposed)
					return;
				if(this.RemainingTasks == 0)
					return;
                this._Finished.WaitOne(1000);
			}
		}

		private void AssertNotDisposed()
		{
			if(this._disposed)
				throw new ObjectDisposedException("CustomThreadPoolTaskScheduler");
		}
	}
}
