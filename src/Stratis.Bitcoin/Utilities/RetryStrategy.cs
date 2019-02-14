using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NLog.LayoutRenderers.Wrappers;

namespace Stratis.Bitcoin.Utilities
{
    public static class RetryStrategy
    {
        /// <summary>
        /// Execute logic that should be retried if failure defined by <c>TException</c> occurs.
        /// </summary>
        /// <param name="retryOptions">Retry options, including number of retries and delay between them.</param>
        /// <param name="actionToExecute">Logic to be executed.</param>
        /// <param name="logger">Optional logger.</param>
        public static void Run(RetryOptions retryOptions, Action actionToExecute, ILogger logger = null)
        {
            if (retryOptions == null || retryOptions.RetryCount == 0)
            {
                actionToExecute();
                return;
            }

            for (int i = 0; i <= retryOptions.RetryCount; i++)
            {
                try
                {
                    actionToExecute();
                    return;
                }
                catch (Exception ex)
                {
                    if (retryOptions.ExceptionTypes.All(et => et != ex.GetType()) || i >= retryOptions.RetryCount) throw;

                    if (logger != null) logger.LogError("Failed to commit transaction. Retrying.", ex);
                    Task.Delay(retryOptions.Delay).GetAwaiter().GetResult();
                }
            }
        }

        /// <summary>
        /// Execute logic that should be retried if failure defined by <c>TException</c> occurs.
        /// </summary>
        /// <param name="retryOptions">Retry options, including number of retries and delay between them.</param>
        /// <param name="actionToExecute">Logic to be executed.</param>
        /// <param name="logger">Optional logger.</param>
        public static TReturn Run<TReturn>(RetryOptions retryOptions, Func<TReturn> actionToExecute, ILogger logger = null)
        {
            if (retryOptions == null || retryOptions.RetryCount == 0)
            {
                return actionToExecute();
            }

            for (int i = 0; i <= retryOptions.RetryCount; i++)
            {
                try
                {
                    TReturn result = actionToExecute();
                    return result;
                }
                catch (Exception ex)
                {
                    if (retryOptions.ExceptionTypes.All(et => et != ex.GetType()) || i >= retryOptions.RetryCount) throw;

                    if (logger != null) logger.LogError("Failed to commit transaction. Retrying.", ex);
                    Task.Delay(retryOptions.Delay).GetAwaiter().GetResult();
                }
            }

            throw new InvalidOperationException("Retry strategy execution failed.");
        }
    }
}