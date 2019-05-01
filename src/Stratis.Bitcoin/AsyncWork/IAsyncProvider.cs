﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.AsyncWork
{
    public interface IAsyncProvider
    {
        /// <summary>
        /// Creates a queue that can be dequeued asynchronously by multiple threads.
        /// </summary>
        /// <typeparam name="T">Type of the queued items.</typeparam>
        /// <returns></returns>
        IAsyncQueue<T> CreateAsyncQueue<T>();

        /// <summary>
        /// Creates a delegate that is called asynchronously in the background whenever a new <typeparamref name="T"/> is queued.
        /// </summary>
        /// <typeparam name="T">Type of the queued items used in the delegate.</typeparam>
        /// <param name="name">Name of the delegate.</param>
        /// <param name="delegate">The delegate.</param>
        /// <returns></returns>
        IAsyncDelegateDequeuer<T> CreateAndRunAsyncDelegateDequeuer<T>(string name, Func<T, CancellationToken, Task> @delegate);

        /// <summary>
        /// Creates an starts an application defined task inside a newly created async loop.
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="loop">The loop.</param>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce"/>, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        /// <returns></returns>
        IAsyncLoop CreateAndRunAsyncLoop(string name, Func<CancellationToken, Task> loop, CancellationToken cancellation, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Creates an starts an application defined task inside a newly created async loop and stop it.
        /// </summary>
        /// <param name="name">Name of the loop.</param>
        /// <param name="cancellation">Cancellation token that triggers when the task and the loop should be cancelled.</param>
        /// <param name="condition">The condition that ensure the loop to continue (true) or stop it(false).</param>
        /// <param name="action">The action to execute in loop.</param>
        /// <param name="onException">Invoked when the condition or the action throws an exception.</param>
        /// <param name="repeatEvery">Interval between each execution of the task.
        /// If this is <see cref="TimeSpans.RunOnce" />, the task is only run once and there is no loop.
        /// If this is null, the task is repeated every 1 second by default.</param>
        /// <param name="startAfter">Delay before the first run of the task, or null if no startup delay is required.</param>
        /// <returns></returns>
        IAsyncLoop CreateAndRunAsyncLoopUntil(string name, CancellationToken cancellation, Func<bool> condition, Action action, Action<Exception> onException, TimeSpan? repeatEvery = null, TimeSpan? startAfter = null);

        /// <summary>
        /// Determines whether an <see cref="IAsyncDelegateDequeuer{T}" /> with the specified name is running.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>
        ///   <c>true</c> if an <see cref="IAsyncDelegateDequeuer{T}" /> with the specified name is currently running, otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Names are not unique, consider adding prefixes to names when <see cref="IAsyncDelegateDequeuer{T}" /> are transient and does not act like singletons. This method is mostly used for tests.
        /// </remarks>
        bool IsAsyncDelegateDequeuerRunning(string name);

        /// <summary>
        /// Determines whether an <see cref="IAsyncLoop" /> with the specified name is running.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>
        ///   <c>true</c> if an <see cref="IAsyncLoop" /> with the specified name is currently running, otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Names are not unique, consider adding prefixes to names when <see cref="IAsyncLoop" /> are transient and does not act like singletons. This method is mostly used for tests.
        /// </remarks>
        bool IsAsyncLoopRunning(string name);

        /// <summary>
        /// Determines whether a specified <see cref="IAsyncDelegate" /> is running.
        /// </summary>
        /// <param name="asyncDelegate">The asynchronous delegate to check.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="IAsyncDelegate" /> is currently running, otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Names are not unique, consider adding prefixes to names when loops are transient and does not act like singletons.
        /// This method is mostly used for tests.
        /// state can be either of type <see cref="IAsyncDelegateDequeuer{T}" /> or <see cref="IAsyncLoop" />
        /// </remarks>
        bool IsAsyncDelegateDequeuerRunning(IAsyncDelegate asyncDelegate);

        /// <summary>
        /// returns statistics about running or faulted async loops.
        /// </summary>
        /// <param name="faultyOnly">if set to <c>true</c> dump information only for loops in faulty state.</param>
        string GetStatistics(bool faultyOnly);
    }
}