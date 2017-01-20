using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
    public class SchedulerPairSession
	{
		public CancellationTokenSource Cancellation { get; private set; }
		private readonly ConcurrentExclusiveSchedulerPair schedulerPair;

		public SchedulerPairSession(CancellationTokenSource cancellation = null)
		{
			this.Cancellation = cancellation ?? new CancellationTokenSource();
			int defaultMaxConcurrencyLevel = Environment.ProcessorCount; // concurrency count
			int defaultMaxitemspertask = 5; // how many exclusive tasks to processes before checking concurrent tasks
			this.schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, defaultMaxConcurrencyLevel, defaultMaxitemspertask);
		}

		/// <summary>
		/// Queue concurrent work to the ConcurrentScheduler.
		/// Delegates calling this method will be done in parallel on the Default scheduler.
		/// </summary>
		public Task<T> DoConcurrent<T>(Func<T> func)
		{
			return Task.Factory.StartNew(func, 
				this.Cancellation.Token, 
				TaskCreationOptions.None, 
				this.schedulerPair.ConcurrentScheduler);
		}

		/// <summary>
		/// Queue sequential work to the ExclusiveScheduler.
		/// Delegates calling this method will be done in sequentially, 
		/// the first task will be queued on the Default scheduler subsequent exclusive tasks will run in that same thread.
		/// </summary>
		public Task<T> DoSequential<T>(Func<T> func)
		{
			return Task.Factory.StartNew(func, 
				this.Cancellation.Token, 
				TaskCreationOptions.None, 
				this.schedulerPair.ExclusiveScheduler);
		}

		/// <summary>
		/// Queue concurrent work to the ConcurrentScheduler.
		/// Delegates calling this method will be done in parallel on the Default scheduler.
		/// </summary>
		public Task DoConcurrent(Action func)
		{
			return Task.Factory.StartNew(func,
				this.Cancellation.Token,
				TaskCreationOptions.None,
				this.schedulerPair.ConcurrentScheduler);
		}

		/// <summary>
		/// Queue sequential work to the ExclusiveScheduler.
		/// Delegates calling this method will be done in sequentially, 
		/// the first task will be queued on the Default scheduler subsequent exclusive tasks will run in that same thread.
		/// </summary>
		public Task DoSequential(Action func)
		{
			return Task.Factory.StartNew(func,
				this.Cancellation.Token,
				TaskCreationOptions.None,
				this.schedulerPair.ExclusiveScheduler);
		}
	}
}
