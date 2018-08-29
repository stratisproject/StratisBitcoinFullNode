using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Asynchronous event handler that can be registered with <see cref="AsyncExecutionEvent{TSender, TArg}"/>.
    /// </summary>
    /// <typeparam name="TSender">Type of the sender object that is the source of the event.</typeparam>
    /// <typeparam name="TArg">Type of the argument that is passed to the callback.</typeparam>
    /// <param name="sender">Source of the event.</param>
    /// <param name="arg">Callback argument.</param>
    public delegate Task AsyncExecutionEventCallback<TSender, TArg>(TSender sender, TArg arg);

    /// <summary>
    /// Execution event is a specific moment in the execution flow of that a component
    /// that other components are allowed to be subscribed to and get notified about
    /// when it occurs.
    /// <para>
    /// This implementation allows components to register asynchronous event handlers.
    /// </para>
    /// </summary>
    /// <typeparam name="TSender">Type of event source sender objects.</typeparam>
    /// <typeparam name="TArg">Type of arguments that are passed to callbacks.</typeparam>
    public class AsyncExecutionEvent<TSender, TArg> : IDisposable
    {
        /// <summary>
        /// Protects access to <see cref="callbackList"/> and <see cref="callbackToListNodeMapping"/>,
        /// and also provides guarantees of <see cref="Unregister(AsyncExecutionEventCallback{TSender, TArg})"/> method.
        /// </summary>
        private readonly AsyncLock asyncLock;

        /// <summary>List of registered callbacks.</summary>
        /// <remarks>All access to this object has to be protected with <see cref="asyncLock"/>.</remarks>
        private readonly LinkedList<AsyncExecutionEventCallback<TSender, TArg>> callbackList;

        /// <summary>Mapping of registered callbacks to nodes of <see cref="callbackList"/> to allow fast lookup during removals.</summary>
        /// <remarks>All access to this object has to be protected with <see cref="asyncLock"/>.</remarks>
        private readonly Dictionary<AsyncExecutionEventCallback<TSender, TArg>, LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>>> callbackToListNodeMapping;

        /// <summary>
        /// Set to <c>true</c> if the current async execution context is the one that executes the callbacks,
        /// set to <c>false</c> otherwise.
        /// </summary>
        /// <remarks>
        /// This allows <see cref="Register(AsyncExecutionEventCallback{TSender, TArg}, bool)"/> and <see cref="Unregister(AsyncExecutionEventCallback{TSender, TArg})"/>
        /// to recognize whether they are executing from a callback or not.
        /// </remarks>
        private readonly AsyncLocal<bool> callbackExecutionInProgress;

        /// <summary>Cancellation source to abort waiting for <see cref="asyncLock"/> after <see cref="Dispose"/> has been executed.</summary>
        private readonly CancellationTokenSource cancellationSource;

        /// <summary>Set to <c>1</c> if <see cref="Dispose"/> was called, <c>0</c> otherwise.</summary>
        private int disposed;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public AsyncExecutionEvent()
        {
            this.asyncLock = new AsyncLock();
            this.callbackList = new LinkedList<AsyncExecutionEventCallback<TSender, TArg>>();
            this.callbackToListNodeMapping = new Dictionary<AsyncExecutionEventCallback<TSender, TArg>, LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>>>();

            this.callbackExecutionInProgress = new AsyncLocal<bool>() { Value = false };
            this.cancellationSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Registers a new callback to be called when an event occurs.
        /// </summary>
        /// <param name="callbackAsync">Callback method to register.</param>
        /// <param name="addFirst"><c>true</c> to insert the new callback as the first callback to be called,
        /// <c>false</c> to add it as the last one.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callbackAsync"/> has already been registered.</exception>
        /// <remarks>
        /// It is allowed that a callback method registers another callback.
        /// </remarks>
        public void Register(AsyncExecutionEventCallback<TSender, TArg> callbackAsync, bool addFirst = false)
        {
            // We only lock if we are outside of the execution context of ExecuteCallbacksAsync.
            IDisposable lockReleaser = !this.callbackExecutionInProgress.Value ? this.asyncLock.Lock(this.cancellationSource.Token) : null;
            try
            {
                if (this.callbackToListNodeMapping.ContainsKey(callbackAsync))
                    throw new ArgumentException("Callback already registered.");

                var node = new LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>>(callbackAsync);

                if (addFirst) this.callbackList.AddFirst(node);
                else this.callbackList.AddLast(node);

                this.callbackToListNodeMapping.Add(callbackAsync, node);
            }
            finally
            {
                lockReleaser?.Dispose();
            }
        }

        /// <summary>
        /// Unregisters an existing callback.
        /// </summary>
        /// <param name="callbackAsync">Callback method to unregister.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callbackAsync"/> was not found among registered callbacks.</exception>
        /// <remarks>
        /// The caller is guaranteed that once this method completes, <paramref name="callbackAsync"/> will not be called by this executor.
        /// <para>It is allowed that a callback method unregisters itself (or another callback).</para>
        /// </remarks>
        public void Unregister(AsyncExecutionEventCallback<TSender, TArg> callbackAsync)
        {
            // We only lock if we are outside of the execution context of ExecuteCallbacksAsync.
            IDisposable lockReleaser = !this.callbackExecutionInProgress.Value ? this.asyncLock.Lock(this.cancellationSource.Token) : null;
            try
            {
                LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>> node;
                if (!this.callbackToListNodeMapping.TryGetValue(callbackAsync, out node))
                    throw new ArgumentException("Trying to unregister callback that is not registered.");

                this.callbackList.Remove(node);
                this.callbackToListNodeMapping.Remove(callbackAsync);
            }
            finally
            {
                lockReleaser?.Dispose();
            }
        }

        /// <summary>
        /// Calls all registered callbacks with the given arguments.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="arg">Argument to pass to the callbacks.</param>
        /// <remarks>
        /// It is necessary to hold the lock while calling the callbacks to provide guarantees described in <see cref="Unregister(AsyncExecutionEventCallback{TSender, TArg})"/>.
        /// However, we do support new callbacks to be registered or unregistered while callbacks are being executed,
        /// but this is only possible from the same execution context - i.e. another task or thread is unable to register or unregister callbacks
        /// while callbacks execution is in progress.
        /// </remarks>
        public async Task ExecuteCallbacksAsync(TSender sender, TArg arg)
        {
            this.callbackExecutionInProgress.Value = true;
            try
            {
                using (await this.asyncLock.LockAsync(this.cancellationSource.Token).ConfigureAwait(false))
                {
                    // We need to make a copy of the list because callbacks may call Register or Unregister,
                    // which modifies the list.
                    var listCopy = new AsyncExecutionEventCallback<TSender, TArg>[this.callbackList.Count];
                    this.callbackList.CopyTo(listCopy, 0);

                    foreach (AsyncExecutionEventCallback<TSender, TArg> callbackAsync in listCopy)
                        await callbackAsync(sender, arg).ConfigureAwait(false);
                }
            }
            finally
            {
                this.callbackExecutionInProgress.Value = false;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// It is allowed that the callback in execution calls this method to dispose the execution event,
        /// in which case, the disposing is deferred after the execution of callbacks is complete.
        /// </remarks>
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
                return;

            if (this.callbackExecutionInProgress.Value)
            {
                // We are currently in the middle of executing callbacks, we can't dispose the async lock now.
                // We need to create a separate task that will attempt to acquire the lock,
                // which can only succeed after the execution of the callbacks is finished.
                Task.Run(() => this.DisposeInternal());
            }
            else
            {
                this.DisposeInternal();
            }
        }

        /// <summary>
        /// Acquires <see cref="asyncLock"/> and disposes resources including the lock.
        /// This lock will never be released, but that is not a problem since it is destroyed.
        /// </summary>
        private void DisposeInternal()
        {
            this.cancellationSource.Cancel();
            this.asyncLock.Lock();

            // Now every waiting thread has been cancelled, we can safely dispose the resources.
            this.asyncLock.Dispose();
            this.cancellationSource.Dispose();
        }
    }
}
