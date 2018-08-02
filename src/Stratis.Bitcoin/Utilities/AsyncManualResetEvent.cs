/*
The MIT License (MIT)

Copyright (c) 2014 StephenCleary

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

Based on the original implementation of AsyncManualResetEvent from
Stephen Cleary's AsyncEx library - https://github.com/StephenCleary/AsyncEx .
 */

using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// An async-compatible manual-reset event.
    /// </summary>
    public sealed class AsyncManualResetEvent
    {
        /// <summary>
        /// Lock to protect access to <see cref="tcs"/>.
        /// </summary>
        private readonly object mutex;

        /// <summary>
        /// The current state of the event.
        /// </summary>
        /// <remarks>All access to this object has to be protected by <see cref="mutex"/>.</remarks>
        private TaskCompletionSource<object> tcs;

        /// <summary>
        /// Creates an async-compatible manual-reset event.
        /// </summary>
        /// <param name="set">Whether the manual-reset event is initially set or unset.</param>
        public AsyncManualResetEvent(bool set)
        {
            this.mutex = new object();
            this.tcs = this.CreateAsyncTaskSource<object>();
            if (set)
                this.tcs.TrySetResult(null);
        }

        /// <summary>
        /// Creates an async-compatible manual-reset event that is initially unset.
        /// </summary>
        public AsyncManualResetEvent()
            : this(false)
        {
        }

        /// <summary>
        /// Whether this event is currently set. This member is seldom used; code using this member has a high possibility of race conditions.
        /// </summary>
        public bool IsSet
        {
            get
            {
                lock (this.mutex)
                {
                    return this.tcs.Task.IsCompleted;
                }
            }
        }

        /// <summary>
        /// Asynchronously waits for this event to be set.
        /// </summary>
        public Task WaitAsync()
        {
            lock (this.mutex)
            {
                return this.tcs.Task;
            }
        }

        /// <summary>
        /// Asynchronously waits for this event to be set or for the wait to be canceled.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token used to cancel the wait. If this token is already canceled, this method will first check whether the event is set.</param>
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            Task waitTask = this.WaitAsync();
            if (waitTask.IsCompleted)
                return;

            if (!cancellationToken.CanBeCanceled)
            {
                await waitTask.ConfigureAwait(false);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<object>();
            using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken), useSynchronizationContext: false))
            {
                await await Task.WhenAny(waitTask, tcs.Task).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sets the event, atomically completing every task returned by <see cref="AsyncManualResetEvent.WaitAsync"/>. If the event is already set, this method does nothing.
        /// </summary>
        public void Set()
        {
            lock (this.mutex)
            {
                this.tcs.TrySetResult(null);
            }
        }

        /// <summary>
        /// Resets the event. If the event is already reset, this method does nothing.
        /// </summary>
        public void Reset()
        {
            lock (this.mutex)
            {
                if (this.tcs.Task.IsCompleted)
                    this.tcs = this.CreateAsyncTaskSource<object>();
            }
        }

        /// <summary>
        /// Creates a new TCS for use with async code, and which forces its continuations to execute asynchronously.
        /// </summary>
        /// <typeparam name="TResult">The type of the result of the TCS.</typeparam>
        private TaskCompletionSource<TResult> CreateAsyncTaskSource<TResult>()
        {
            return new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
