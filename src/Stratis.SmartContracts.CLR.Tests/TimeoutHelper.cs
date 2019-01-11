using System;
using System.Threading.Tasks;

namespace Stratis.SmartContracts.CLR.Tests
{
    public static class TimeoutHelper
    {
        /// <summary>
        /// Throw an exception if a task isn't completed within the given amount of seconds.
        /// </summary>
        public static T RunCodeWithTimeout<T>(int timeout, Func<T> execute)
        {
            // ref. https://stackoverflow.com/questions/20282111/xunit-net-how-can-i-specify-a-timeout-how-long-a-test-should-maximum-need
            // Only run single-threaded code in this method

            Task<T> task = Task.Run(execute);
            bool completedInTime = Task.WaitAll(new Task[] { task }, TimeSpan.FromSeconds(timeout));

            if (task.Exception != null)
            {
                if (task.Exception.InnerExceptions.Count == 1)
                {
                    throw task.Exception.InnerExceptions[0];
                }

                throw task.Exception;
            }

            if (!completedInTime)
            {
                throw new TimeoutException($"Task did not complete in {timeout} seconds.");
            }

            return task.Result;
        }
    }
}
