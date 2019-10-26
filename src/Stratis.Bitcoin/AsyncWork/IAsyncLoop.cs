using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.AsyncWork
{
    /// <summary>
    /// Allows running application defined in a loop with specific timing.
    /// </summary>
    public interface IAsyncLoop : IAsyncDelegate
    {
        /// <summary>Name of the loop. It is used for logging.</summary>
        string Name { get; }

        /// <summary>Interval between each execution of the task.</summary>
        TimeSpan RepeatEvery { get; set; }

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        IAsyncLoop Run(TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Starts an application defined task inside the async loop.
        /// </summary>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        IAsyncLoop Run(CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// The task representing the loop being executed.
        /// </summary>
        Task RunningTask { get; }
    }
}
