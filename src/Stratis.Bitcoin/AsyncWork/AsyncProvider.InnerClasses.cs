using System;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.AsyncWork
{
    public partial class AsyncProvider : IAsyncProvider
    {
        /// <summary>
        /// Interface used to allow BackgroundWorkProvider to set private properties
        /// </summary>
        private interface IAsyncTaskInfoSetter
        {
            TaskStatus Status { set; }

            Exception Exception { set; }
        }

        /// <summary>
        /// Class that holds the status of running or faulted async delegate created by the BackgroundWorkProvider
        /// </summary>
        /// <seealso cref="Stratis.Bitcoin.AsyncWork.AsyncProvider.IAsyncTaskInfoSetter" />
        internal class AsyncTaskInfo : IAsyncTaskInfoSetter
        {
            internal enum AsyncTaskType
            {
                /// <summary> Refers to an <see cref="IAsyncLoop"/>.</summary>
                Loop,

                /// <summary> Refers to an <see cref="IAsyncDelegateDequeuer{T}"/>.</summary>
                Dequeuer,

                /// <summary> Refers to a registered <see cref="Task"/>.</summary>
                RegisteredTask
            }

            public string FriendlyName { get; }

            /// <summary>
            /// Gets the status of the task running the delegate.
            /// </summary>
            public TaskStatus Status { get; private set; }

            public Exception Exception { get; private set; }

            TaskStatus IAsyncTaskInfoSetter.Status { set => this.Status = value; }

            Exception IAsyncTaskInfoSetter.Exception { set => this.Exception = value; }

            /// <summary>
            /// Specifies which type of async worker this instance contains information about.
            /// </summary>
            public AsyncTaskType Type { get; }

            public bool IsRunning => this.Status != TaskStatus.Faulted;

            /// <summary>
            /// Initializes a new instance of the <see cref="AsyncTaskInfo"/> class.
            /// </summary>
            /// <param name="friendlyName">Friendly name of the async delegate.</param>
            /// <param name="isDelegateWorker">if set to <c>true</c> the information represents an <see cref="IAsyncDelegateDequeuer"/>, otherwise an <see cref="IAsyncLoop"/>.</param>
            public AsyncTaskInfo(string friendlyName, AsyncTaskType type)
            {
                this.FriendlyName = friendlyName;
                this.Type = type;
            }
        }
    }
}