using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Utilities
{
    /// <summary>
    /// Asynchronous event handler that can be registered with <see cref="AsyncExecutionEvent{TSender, TArg}/>.
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
    public class AsyncExecutionEvent<TSender, TArg>: IDisposable
    {
        /// <summary>
        /// Protects access to <see cref="callbackList"/> and <see cref="callbackToListNodeMapping"/>,
        /// and also provides gurantees of <see cref="Unregister(T)"/> method.
        /// </summary>
        private readonly AsyncLock lockObject;

        /// <summary>List of registered callbacks.</summary>
        /// <remarks>All access to this object has to be protected with <see cref="lockObject"/>.</remarks>
        private readonly LinkedList<AsyncExecutionEventCallback<TSender, TArg>> callbackList;

        /// <summary>Mapping of registered callbacks to nodes of <see cref="callbackList"/> to allow fast lookup during removals.</summary>
        /// <remarks>All access to this object has to be protected with <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<AsyncExecutionEventCallback<TSender, TArg>, LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>>> callbackToListNodeMapping;

        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public AsyncExecutionEvent()
        {
            this.lockObject = new AsyncLock();
            this.callbackList = new LinkedList<AsyncExecutionEventCallback<TSender, TArg>>();
            this.callbackToListNodeMapping = new Dictionary<AsyncExecutionEventCallback<TSender, TArg>, LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>>>();
        }

        /// <summary>
        /// Registers a new callback to be called when an event occurs.
        /// </summary>
        /// <param name="callbackAsync">Callback method to register.</param>
        /// <param name="addFirst"><c>true</c> to insert the new callback as the first callback to be called,
        /// <c>false</c> to add it as the last one.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callbackAsync"/> has already been registered.</exception>
        public void Register(AsyncExecutionEventCallback<TSender, TArg> callbackAsync, bool addFirst = false)
        {
            using (this.lockObject.Lock())
            {
                if (this.callbackToListNodeMapping.ContainsKey(callbackAsync))
                    throw new ArgumentException("Callback already registered.");

                var node = new LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>>(callbackAsync);

                if (addFirst) this.callbackList.AddFirst(node);
                else this.callbackList.AddLast(node);

                this.callbackToListNodeMapping.Add(callbackAsync, node);
            }
        }

        /// <summary>
        /// Unregisters an existing callback.
        /// </summary>
        /// <param name="callbackAsync">Callback method to unregister.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="callbackAsync"/> was not found among registered callbacks.</exception>
        /// <remarks>
        /// The caller is guaranteed that once this method completes, <paramref name="callbackAsync"/> will not be called by this executor.
        /// </remarks>
        public void Unregister(AsyncExecutionEventCallback<TSender, TArg> callbackAsync)
        {
            using (this.lockObject.Lock())
            {
                LinkedListNode<AsyncExecutionEventCallback<TSender, TArg>> node;
                if (!this.callbackToListNodeMapping.TryGetValue(callbackAsync, out node))
                    throw new ArgumentException("Trying to unregistered callback that is not registered.");

                this.callbackList.Remove(node);
                this.callbackToListNodeMapping.Remove(callbackAsync);
            }
        }

        /// <summary>
        /// Calls all registered callbacks with the given arguments.
        /// </summary>
        /// <param name="sender">Source of the event.</param>
        /// <param name="arg">Argument to pass to the callbacks.</param>
        /// <remarks>
        /// It is necessary to hold the lock while calling the callbacks to provide gurantees described in <see cref="Unregister(AsyncExecutionEventCallback{T})"/>.
        /// This means no new callback can be registered or unregistered while callbacks are being executed.
        /// </remarks>
        public async Task ExecuteCallbacksAsync(TSender sender, TArg arg)
        {
            using (await this.lockObject.LockAsync().ConfigureAwait(false))
            {
                foreach (AsyncExecutionEventCallback<TSender, TArg> callbackAsync in this.callbackList)
                    await callbackAsync(sender, arg).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.lockObject.Dispose();
        }
    }
}
