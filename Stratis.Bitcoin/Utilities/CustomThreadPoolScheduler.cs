using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Task scheduler that spawns an exact number of threads for execution of its tasks.
    /// </summary>
    public class CustomThreadPoolTaskScheduler : TaskScheduler, IDisposable
    {
        /// <summary>Number of threads that the scheduler can use to execute the tasks.</summary>
        private int threadCount;
        /// <summary>Number of threads that the scheduler can use to execute the tasks.</summary>
        public int ThreadCount
        {
            get
            {
                return this.threadCount;
            }
        }

        /// <summary>
        /// Cancellation token that causes the scheduler threads to exit their loops. 
        /// It is triggered when the instance of the scheduler is disposed.
        /// </summary>
        private CancellationTokenSource cancel = new CancellationTokenSource();

        /// <summary>Event that signals when no tasks are executing and there are no more tasks in the queue to be executed.</summary>
        /// <remarks>TODO: It seems that this event is used in a broken way. First, it does not signal by default, which probably means 
        /// that unless a first task is scheduled, it can't signal. Second, it is set in <see cref="Do"/> method if after executing 
        /// the task there are no more tasks to execute or being executed at the moment. However, assume a case in which no one 
        /// waits on this event. This means that after first task completes, no one resets the event and even if there later are 
        /// many tasks being executed or in the queue, this event would still be set until someone would consume it, which would 
        /// immediately cause the consumer to think that there is no more work to be done.
        /// <para>
        /// Moreover, there seem to be no synchronization over the shared resource among multiple threads, 
        /// so race conditions are possible.
        /// </para>
        /// <para>
        /// As no one is using <see cref="WaitFinished"/> anyway, I suggest to remove both, this event and the method using it 
        /// from the code.
        /// </para>
        /// </remarks>
        private AutoResetEvent finished = new AutoResetEvent(false);

        /// <summary>Number of scheduler threads that are currently not busy and can be used for task execution.</summary>
        private int availableThreads;
        /// <summary>Number of scheduler threads that are currently not busy and can be used for task execution.</summary>
        public int AvailableThreads
        {
            get
            {
                return this.availableThreads;
            }
        }

        /// <summary>Blocking queue of tasks to be executed by this scheduler.</summary>
        private BlockingCollection<Task> tasks;

        /// <summary>
        /// Initializes a new instance of the object with a specific number of threads that 
        /// the scheduler can use and a maximum limit on the capacity of the task queue.
        /// </summary>
        /// <param name="threadCount">Number of threads that the scheduler can use to execute the tasks.</param>
        /// <param name="maxQueued">Maximum number of queued tasks.</param>
        /// <param name="name">Name of the thread that is responsible for executing the tasks by this scheduler. See <see cref="Thread.Name"/>.</param>
        public CustomThreadPoolTaskScheduler(int threadCount, int maxQueued, string name = null)
        {
            this.threadCount = threadCount;
            this.tasks = new BlockingCollection<Task>(new ConcurrentQueue<Task>(), maxQueued);
            this.availableThreads = threadCount;
            for (int i = 0; i < threadCount; i++)
            {
                new Thread(Do)
                {
                    IsBackground = true,
                    Name = name
                }.Start();
            }
        }

        /// <inheritdoc />
        public override int MaximumConcurrencyLevel
        {
            get
            {
                return this.threadCount;
            }
        }

        /// <summary>Number of unfinished tasks that are waiting to be executed.</summary>
        public int QueuedCount
        {
            get
            {
                return this.tasks.Count;
            }
        }

        /// <summary>Number of tasks that remains to be finished. This is number of queued tasks waiting for execution plus number of currently executing tasks.</summary>
        public int RemainingTasks
        {
            get
            {
                return (this.threadCount - this.AvailableThreads) + this.QueuedCount;
            }
        }

        /// <summary>
        /// Thread procedure that consumes the tasks added to the queue and executes them.
        /// <para>There are <see cref="threadCount"/> number of threads executing this method at the same time.</para>
        /// </summary>
        void Do()
        {
            try
            {
                foreach (Task task in this.tasks.GetConsumingEnumerable(this.cancel.Token))
                {
                    Interlocked.Decrement(ref this.availableThreads);
                    TryExecuteTask(task);
                    Interlocked.Increment(ref this.availableThreads);
                    if (this.RemainingTasks == 0)
                        this.finished.Set();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <inheritdoc />
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return this.tasks;
        }

        /// <inheritdoc />
        /// <remarks>This scheduler is limited to accept only certain amount of unfinished tasks. 
        /// See maxQueued in <see cref="CustomThreadPoolTaskScheduler"/> constructor. If the queue 
        /// is full, this function blocks until the tasks in the queue are processed.</remarks>
        protected override void QueueTask(Task task)
        {
            AssertNotDisposed();
            this.tasks.Add(task);
        }

        /// <inheritdoc />
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            AssertNotDisposed();
            return false;
        }

        /// <summary>
        /// Waits until all tasks are finished or the object is disposed.
        /// </summary>
        /// <remarks>TODO: This method relies on faulty use of <see cref="finished"/>. No one is using this method. Therefore I'd suggest to remove it from the code.</remarks>
        public void WaitFinished()
        {
            AssertNotDisposed();
            while (true)
            {
                if (this.disposed)
                    return;

                if (this.RemainingTasks == 0)
                    return;

                this.finished.WaitOne(1000);
            }
        }

        /// <summary>
        /// Throws exception if the instance of the object has been disposed.
        /// </summary>
        private void AssertNotDisposed()
        {
            if (this.disposed)
                throw new ObjectDisposedException("CustomThreadPoolTaskScheduler");
        }

        #region IDisposable Members

        /// <summary>true if the instance of the object has been disposed.</summary>
        private bool disposed;
        
        /// <summary>
        /// Disposes the object, triggers the cancallation token.
        /// </summary>
        public void Dispose()
        {
            this.disposed = true;
            this.cancel.Cancel();
        }

        #endregion
    }
}
