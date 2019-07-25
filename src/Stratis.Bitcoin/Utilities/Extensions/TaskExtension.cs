using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin
{
    public static class TaskExtensions
    {
        /// <summary>
        /// Allows to cancel awaitable operations with a cancellationToken.
        /// https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
        /// </summary>
        /// <typeparam name="T">Task return type</typeparam>
        /// <param name="task">The task.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">Task has been cancelled.</exception>
        public static async Task<T> WithCancellationAsync<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                    throw new OperationCanceledException(cancellationToken);

            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Allows to cancel awaitable operations with a cancellationToken.
        /// https://devblogs.microsoft.com/pfxteam/how-do-i-cancel-non-cancelable-async-operations/
        /// </summary>
        /// <param name="task">The task.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="OperationCanceledException">Task has been cancelled.</exception>
        public static async Task WithCancellationAsync(this Task task, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false))
                    throw new OperationCanceledException(cancellationToken);

            await task.ConfigureAwait(false); // This is needed to rethrow eventual task exception.
        }
    }
}
