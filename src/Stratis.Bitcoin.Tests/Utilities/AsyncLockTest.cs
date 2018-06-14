using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Tests.Utilities
{
    /// <summary>
    /// Tests of <see cref="AsyncLock"/> class.
    /// </summary>
    public class AsyncLockTest
    {
        /// <summary>Context information for each worker executed in separate task.</summary>
        public class WorkerContext : IDisposable
        {
            /// <summary>The lock object being tested.</summary>
            public AsyncLock Lock { get; set; }

            /// <summary>Source of randomness.</summary>
            public Random Rng { get; set; }

            /// <summary>Value that is accessed and modified by different threads. Used for detection of critical section execution violations.</summary>
            public int SharedValue { get; set; }

            /// <summary>If an error occurs, this is set to <c>true</c> and the test fails.</summary>
            public bool Error { get; set; }

            /// <summary>Optionally, a cancellation token source that triggers after certain time.</summary>
            public CancellationTokenSource Cancellation { get; set; }

            /// <summary>Identifier of the worker that is increment by each new worker.</summary>
            private int workerId;

            /// <summary>
            /// Initializes default values and objects.
            /// </summary>
            /// <param name="cancelAfterMaxMs">Maximal number of milliseconds after which the cancellation triggers (the actual number will be picked at random),
            /// or <c>0</c> if no cancellation is required.</param>
            public WorkerContext(int cancelAfterMaxMs = 0)
            {
                this.Lock = new AsyncLock();
                this.Rng = new Random();
                this.SharedValue = 0;
                this.Error = false;
                this.workerId = 0;

                if (cancelAfterMaxMs != 0)
                    this.Cancellation = new CancellationTokenSource(this.Rng.Next(cancelAfterMaxMs));
            }

            /// <summary>
            /// Generates a unique ID for the worker.
            /// </summary>
            /// <returns>Worker's unique ID.</returns>
            public int GetWorkerId()
            {
                return Interlocked.Increment(ref this.workerId);
            }

            /// <inheritdoc />
            public void Dispose()
            {
                this.Cancellation?.Dispose();
            }
        }

        /// <summary>
        /// Creates 20 parallel tasks, each trying to enter a critical section.
        /// 10 tasks are testing the lock in an async environment and the other 10 tasks in non-async environment.
        /// <para>
        /// The test is passed if the critical section is always correctly executed by only one thread.
        /// The violations are detected using a shared value that each worker tries to set to its own unique
        /// value and the value should not be rewritten by other thread while the worker is in the critical section.
        /// </para>
        /// </summary>
        [Fact]
        public void LockAndLockAsync_PreventConcurrentExecution()
        {
            var context = new WorkerContext();

            int taskCount = 20;
            var tasks = new Task[taskCount];
            for (int i = 0; i < taskCount; i += 2)
            {
                // Start one task that will use the lock in async environment.
                tasks[i] = Task.Run(async () => { await this.LockAndLockAsync_PreventConcurrentExecution_TaskProcAsync(context); });

                int delay = context.Rng.Next(500);
                Thread.Sleep(delay);

                // Start one task that will use the lock in non-async environment.
                tasks[i + 1] = Task.Run(() => this.LockAndLockAsync_PreventConcurrentExecution_TaskProc(context));

                delay = context.Rng.Next(200);
                Thread.Sleep(delay);
            }

            Task.WaitAll(tasks);
            Assert.False(context.Error);
            context.Lock.Dispose();
        }

        /// <summary>
        /// Procedure for testing the lock in async environment.
        /// </summary>
        /// <param name="workerContext">Shared information the worker needs for the task.</param>
        private async Task LockAndLockAsync_PreventConcurrentExecution_TaskProcAsync(WorkerContext workerContext)
        {
            using (await workerContext.Lock.LockAsync())
            {
                await this.CriticalSectionLockedAsync(workerContext);
            }
        }

        /// <summary>
        /// Procedure for testing the lock in non-async environment.
        /// <para>
        /// The worker periodically sets the shared value to its ID and waits.
        /// If another thread enters the critical section, the shared value will be modified
        /// and the worker reports an error.
        /// </para>
        /// </summary>
        /// <param name="workerContext">Shared information the worker needs for the task.</param>
        private void LockAndLockAsync_PreventConcurrentExecution_TaskProc(WorkerContext workerContext)
        {
            using (workerContext.Lock.Lock())
            {
                this.CriticalSectionLocked(workerContext);
            }
        }

        /// <summary>
        /// This is very similar test to <see cref="LockAndLockAsync_PreventConcurrentExecution"/> except that it
        /// introduces a cancellation token to the mix.
        /// </summary>
        [Fact]
        public void LockAndLockAsync_WithCancellationToken_PreventConcurrentExecution()
        {
            var context = new WorkerContext(5000);

            int taskCount = 20;
            var tasks = new Task[taskCount];
            for (int i = 0; i < taskCount; i += 2)
            {
                // Start one task that will use the lock in async environment.
                tasks[i] = Task.Run(async () => { await this.LockAndLockAsync_WithCancellationToken_PreventConcurrentExecution_TaskProcAsync(context); });

                int delay = context.Rng.Next(500);
                Thread.Sleep(delay);

                // Start one task that will use the lock in non-async environment.
                tasks[i + 1] = Task.Run(() => this.LockAndLockAsync_WithCancellationToken_PreventConcurrentExecution_TaskProc(context));

                delay = context.Rng.Next(200);
                Thread.Sleep(delay);
            }

            Task.WaitAll(tasks);
            Assert.False(context.Error);
            context.Lock.Dispose();
        }

        /// <summary>
        /// Procedure for testing the lock in async environment.
        /// </summary>
        /// <param name="workerContext">Shared information the worker needs for the task.</param>
        private async Task LockAndLockAsync_WithCancellationToken_PreventConcurrentExecution_TaskProcAsync(WorkerContext workerContext)
        {
            try
            {
                using (await workerContext.Lock.LockAsync(workerContext.Cancellation.Token))
                {
                    await this.CriticalSectionLockedAsync(workerContext);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Procedure for testing the lock in non-async environment.
        /// <para>
        /// The worker periodically sets the shared value to its ID and waits.
        /// If another thread enters the critical section, the shared value will be modified
        /// and the worker reports an error.
        /// </para>
        /// </summary>
        /// <param name="workerContext">Shared information the worker needs for the task.</param>
        private void LockAndLockAsync_WithCancellationToken_PreventConcurrentExecution_TaskProc(WorkerContext workerContext)
        {
            try
            {
                using (workerContext.Lock.Lock(workerContext.Cancellation.Token))
                {
                    this.CriticalSectionLocked(workerContext);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Body of critical section for async environment.
        /// <para>
        /// The worker periodically sets the shared value to its ID and waits.
        /// If another thread enters the critical section, the shared value will be modified
        /// and the worker reports an error.
        /// </para>
        /// </summary>
        /// <param name="workerContext">Shared information the worker needs for the task.</param>
        /// <remarks>The caller is responsible for holding <see cref="WorkerContext.Lock"/>.</remarks>
        private async Task CriticalSectionLockedAsync(WorkerContext workerContext)
        {
            int id = workerContext.GetWorkerId();
            for (int i = 0; i < 5; i++)
            {
                workerContext.SharedValue = id;

                int delay = workerContext.Rng.Next(200);
                await Task.Delay(delay);

                // If other thread enters the critical section, the error will be set.
                workerContext.Error |= workerContext.SharedValue != id;
            }
        }

        /// <summary>
        /// Body of critical section for non-async environment.
        /// <para>
        /// The worker periodically sets the shared value to its ID and waits.
        /// If another thread enters the critical section, the shared value will be modified
        /// and the worker reports an error.
        /// </para>
        /// </summary>
        /// <param name="workerContext">Shared information the worker needs for the task.</param>
        /// <remarks>The caller is responsible for holding <see cref="WorkerContext.Lock"/>.</remarks>
        private void CriticalSectionLocked(WorkerContext workerContext)
        {
            int id = workerContext.GetWorkerId();
            for (int i = 0; i < 5; i++)
            {
                workerContext.SharedValue = id;

                int delay = workerContext.Rng.Next(200);
                Thread.Sleep(delay);

                // If other thread enters the critical section, the error will be set.
                workerContext.Error |= workerContext.SharedValue != id;
            }
        }
    }
}
