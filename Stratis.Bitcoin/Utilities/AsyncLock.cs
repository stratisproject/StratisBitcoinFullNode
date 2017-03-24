using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    public interface IAsyncLock
    {
        CancellationTokenSource Cancellation { get; }

        Task ReadAsync(Action func);
        Task<T> ReadAsync<T>(Func<T> func);
        Task WriteAsync(Action func);
        Task<T> WriteAsync<T>(Func<T> func);
    }

    /// <summary>
    /// An async reader writer lock for concurrent and exclusive work
    /// </summary>
    /// <remarks>
    /// From the TaskFactory.StartNew() remarks:
    /// Calling StartNew is functionally equivalent to creating a Task using one of its constructors 
    /// and then calling 
    /// <see cref="System.Threading.Tasks.Task.Start()">Start</see> to schedule it for execution.  However,
    /// unless creation and scheduling must be separated, StartNew is the recommended
    /// approach for both simplicity and performance.
    /// </remarks>
    public class AsyncLock : IAsyncLock
    {
		public CancellationTokenSource Cancellation { get; private set; }
		private readonly ConcurrentExclusiveSchedulerPair schedulerPair; // reference kept for perf counter

		private readonly TaskFactory concurrentFactory;
		private readonly TaskFactory exclusiveFactory;

		public AsyncLock(CancellationTokenSource cancellation = null, int? maxitemspertask = null)
		{
			this.Cancellation = cancellation ?? new CancellationTokenSource();
			int defaultMaxConcurrencyLevel = Environment.ProcessorCount; // concurrency count
			int defaultMaxitemspertask = maxitemspertask ?? 5; // how many exclusive tasks to processes before checking concurrent tasks
			this.schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, defaultMaxConcurrencyLevel, defaultMaxitemspertask);
			this.concurrentFactory = new TaskFactory(this.schedulerPair.ConcurrentScheduler);
			this.exclusiveFactory = new TaskFactory(this.schedulerPair.ExclusiveScheduler);
		}

		/// <summary>
		/// Queue concurrent work to the ConcurrentScheduler.
		/// Delegates calling this method will be done in parallel on the Default scheduler.
		/// </summary>
		public Task<T> ReadAsync<T>(Func<T> func)
		{
			return this.concurrentFactory.StartNew(func, this.Cancellation.Token);
		}

		/// <summary>
		/// Queue sequential work to the ExclusiveScheduler.
		/// Delegates calling this method will be done in sequentially, 
		/// the first task will be queued on the Default scheduler subsequent exclusive tasks will run in that same thread.
		/// </summary>
		public Task<T> WriteAsync<T>(Func<T> func)
		{
			return this.exclusiveFactory.StartNew(func, this.Cancellation.Token);
		}

		/// <summary>
		/// Queue concurrent work to the ConcurrentScheduler.
		/// Delegates calling this method will be done in parallel on the Default scheduler.
		/// </summary>
		public Task ReadAsync(Action func)
		{
			return this.concurrentFactory.StartNew(func, this.Cancellation.Token);
		}

		/// <summary>
		/// Queue sequential work to the ExclusiveScheduler.
		/// Delegates calling this method will be done in sequentially, 
		/// the first task will be queued on the Default scheduler subsequent exclusive tasks will run in that same thread.
		/// </summary>
		public Task WriteAsync(Action func)
		{
			return this.exclusiveFactory.StartNew(func, this.Cancellation.Token);
		}
	}
}
