using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// An async reader writer lock for concurrent and exclusive work.
    /// </summary>
    public interface ISchedulerLock
    {
        /// <summary>
        /// Queues concurrent work to the concurrent scheduler.
        /// Delegates calling this method will be done in parallel on the default scheduler.
        /// </summary>
        /// <param name="func">Method to be called with locked reader lock.</param>
        Task ReadAsync(Action func);

        /// <summary>
        /// Queues concurrent work to the concurrent scheduler.
        /// Delegates calling this method will be done in parallel on the default scheduler.
        /// </summary>
        /// <typeparam name="T">Return type of the delegated method.</typeparam>
        /// <param name="func">Method to be called with locked reader lock.</param>
        /// <returns>Return value of the delegated method.</returns>
        Task<T> ReadAsync<T>(Func<T> func);

        /// <summary>
        /// Queues sequential work to the exclusive scheduler.
        /// Delegates calling this method will be done in sequentially,
        /// The first task will be queued on the default scheduler subsequent exclusive tasks will run in that same thread.
        /// </summary>
        /// <param name="func">Method to be called with locked writer lock.</param>
        Task WriteAsync(Action func);

        /// <summary>
        /// Queues sequential work to the exclusive scheduler.
        /// Delegates calling this method will be done in sequentially,
        /// The first task will be queued on the default scheduler subsequent exclusive tasks will run in that same thread.
        /// </summary>
        /// <typeparam name="T">Return type of the delegated method.</typeparam>
        /// <param name="func">Method to be called with locked writer lock.</param>
        /// <returns>Return value of the delegated method.</returns>
        Task<T> WriteAsync<T>(Func<T> func);
    }

    /// <summary>
    /// An async reader writer lock for concurrent and exclusive work.
    /// <para>
    /// The class uses ConcurrentExclusiveSchedulerPair to access two task schedulers - concurrent
    /// scheduler and exclusive scheduler. The exclusive scheduler guarantees only one task to be run
    /// at the same, which is what is used as a writer lock. The concurrent scheduler allows multiple
    /// tasks to run simultaneously, but the exclusivity of exclusive scheduler is respected, so it is
    /// used as a reader lock.
    /// </para>
    /// </summary>
    /// <remarks>
    /// From the TaskFactory.StartNew() remarks:
    /// Calling StartNew is functionally equivalent to creating a Task using one of its constructors
    /// and then calling <see cref="Task.Start()">Start</see> to schedule it for execution. However,
    /// unless creation and scheduling must be separated, StartNew is the recommended approach for both
    /// simplicity and performance.
    /// <para>
    /// WARNING: One has to be very careful using this class as the exclusivity of the exclusive scheduler
    /// only guarantees to actually run one task at the time, but if the task awaits, it is not considered
    /// as running and another task can be scheduled and run instead within the context of the exclusive
    /// scheduler. This means that the tasks run within both exclusive and concurrent schedulers
    /// must not await, otherwise there is a risk of a race condition. Thus in order to use this locking
    /// mechanism, one needs to first break up the asynchronous code to synchronous pieces and only then
    /// schedule the synchronous parts.
    /// </para>
    /// </remarks>
    public class SchedulerLock : ISchedulerLock
    {
        /// <inheritdoc />
        private CancellationTokenSource cancellation;

        /// <summary>Task factory that runs tasks using the concurrent scheduler. Serves as a reader lock.</summary>
        private readonly TaskFactory concurrentFactory;

        /// <summary>Task factory that runs tasks using the exclusive scheduler. Serves as a writer lock.</summary>
        private readonly TaskFactory exclusiveFactory;

        /// <summary>
        /// Initializes a new instance of the object with ability to cancel locked tasks.
        /// </summary>
        /// <param name="cancellation">Cancellation source to allow cancel the tasks run by the schedulers.</param>
        /// <param name="maxItemsPerTask">Number of exclusive tasks to process before checking concurrent tasks.</param>
        public SchedulerLock(CancellationTokenSource cancellation = null, int maxItemsPerTask = 5)
        {
            this.cancellation = cancellation ?? new CancellationTokenSource();
            int defaultMaxConcurrencyLevel = Environment.ProcessorCount;
            int defaultMaxItemsPerTask = maxItemsPerTask;
            var schedulerPair = new ConcurrentExclusiveSchedulerPair(TaskScheduler.Default, defaultMaxConcurrencyLevel, defaultMaxItemsPerTask);
            this.concurrentFactory = new TaskFactory(schedulerPair.ConcurrentScheduler);
            this.exclusiveFactory = new TaskFactory(schedulerPair.ExclusiveScheduler);
        }

        /// <inheritdoc />
        public Task ReadAsync(Action func)
        {
            return this.concurrentFactory.StartNew(func, this.cancellation.Token);
        }

        /// <inheritdoc />
        /// <remarks>See warning in <see cref="SchedulerLock"/> remarks section.</remarks>
        public Task<T> ReadAsync<T>(Func<T> func)
        {
            return this.concurrentFactory.StartNew(func, this.cancellation.Token);
        }

        /// <inheritdoc />
        /// <remarks>See warning in <see cref="SchedulerLock"/> remarks section.</remarks>
        public Task WriteAsync(Action func)
        {
            return this.exclusiveFactory.StartNew(func, this.cancellation.Token);
        }

        /// <inheritdoc />
        /// <remarks>See warning in <see cref="SchedulerLock"/> remarks section.</remarks>
        public Task<T> WriteAsync<T>(Func<T> func)
        {
            return this.exclusiveFactory.StartNew(func, this.cancellation.Token);
        }
    }
}
