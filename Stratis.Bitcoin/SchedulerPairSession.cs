using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
	/// <summary>
	/// A scheduler session for concurrent and exclusive work
	/// </summary>
	/// <remarks>
	/// From the TaskFactory.StartNew() remarks:
	/// Calling StartNew is functionally equivalent to creating a Task using one of its constructors 
	/// and then calling 
	/// <see cref="System.Threading.Tasks.Task.Start()">Start</see> to schedule it for execution.  However,
	/// unless creation and scheduling must be separated, StartNew is the recommended
	/// approach for both simplicity and performance.
	/// </remarks>
	public class SchedulerPairSession
	{
		public CancellationTokenSource Cancellation { get; private set; }
		private readonly ConcurrentExclusiveSchedulerPair schedulerPair; // reference kept for perf counter

		private readonly TaskFactory concurrentFactory;
		private readonly TaskFactory exclusiveFactory;

		public SchedulerPairSession(CancellationTokenSource cancellation = null)
		{
			this.Cancellation = cancellation ?? new CancellationTokenSource();
			int defaultMaxConcurrencyLevel = Environment.ProcessorCount; // concurrency count
			int defaultMaxitemspertask = 5; // how many exclusive tasks to processes before checking concurrent tasks
			this.schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, defaultMaxConcurrencyLevel, defaultMaxitemspertask);
			this.concurrentFactory = new TaskFactory(this.schedulerPair.ConcurrentScheduler);
			this.exclusiveFactory = new TaskFactory(this.schedulerPair.ExclusiveScheduler);
		}

		/// <summary>
		/// Queue concurrent work to the ConcurrentScheduler.
		/// Delegates calling this method will be done in parallel on the Default scheduler.
		/// </summary>
		public Task<T> DoConcurrent<T>(Func<T> func)
		{
			return this.concurrentFactory.StartNew(func, this.Cancellation.Token);
		}

		/// <summary>
		/// Queue sequential work to the ExclusiveScheduler.
		/// Delegates calling this method will be done in sequentially, 
		/// the first task will be queued on the Default scheduler subsequent exclusive tasks will run in that same thread.
		/// </summary>
		public Task<T> DoSequential<T>(Func<T> func)
		{
			return this.exclusiveFactory.StartNew(func, this.Cancellation.Token);
		}

		/// <summary>
		/// Queue concurrent work to the ConcurrentScheduler.
		/// Delegates calling this method will be done in parallel on the Default scheduler.
		/// </summary>
		public Task DoConcurrent(Action func)
		{
			return this.concurrentFactory.StartNew(func, this.Cancellation.Token);
		}

		/// <summary>
		/// Queue sequential work to the ExclusiveScheduler.
		/// Delegates calling this method will be done in sequentially, 
		/// the first task will be queued on the Default scheduler subsequent exclusive tasks will run in that same thread.
		/// </summary>
		public Task DoSequential(Action func)
		{
			return this.exclusiveFactory.StartNew(func, this.Cancellation.Token);
		}
	}
}
