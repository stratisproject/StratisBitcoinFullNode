using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Base.BackgroundWork
{
    public partial class BackgroundWorkProvider : IBackgroundWorkProvider
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
        /// <seealso cref="Stratis.Bitcoin.Base.BackgroundWork.BackgroundWorkProvider.IAsyncTaskInfoSetter" />
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