using System;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Base.AsyncWork
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
        /// <seealso cref="Stratis.Bitcoin.Base.AsyncWork.AsyncProvider.IAsyncTaskInfoSetter" />
        internal class AsyncTaskInfo : IAsyncTaskInfoSetter
        {
            public string FriendlyName { get; }

            public TaskStatus Status { get; private set; }

            public Exception Exception { get; private set; }

            TaskStatus IAsyncTaskInfoSetter.Status { set => this.Status = value; }

            Exception IAsyncTaskInfoSetter.Exception { set => this.Exception = value; }

            public AsyncTaskInfo(string friendlyName)
            {
                this.FriendlyName = friendlyName;
            }
        }
    }
}