using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.Utilities
{
    public static class RetryStrategy
    {
        public static void Run<T>(RetryOptions retryOptions, Action actionToExecute, ILogger logger = null) where T : Exception
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
                catch (T ex)
                {
                    if (i >= retryOptions.RetryCount) throw;

                    if (logger != null) logger.LogError("Failed to commit transaction. Retrying.", ex);
                    Task.Delay(retryOptions.Delay).GetAwaiter().GetResult();
                }
            }
        }

        public static TReturn Run<T, TReturn>(RetryOptions retryOptions, Func<TReturn> actionToExecute, ILogger logger = null) where T : Exception
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
                catch (T ex)
                {
                    if (i >= retryOptions.RetryCount) throw;

                    if (logger != null) logger.LogError("Failed to commit transaction. Retrying.", ex);
                    Task.Delay(retryOptions.Delay).GetAwaiter().GetResult();
                }
            }

            throw default(T);
        }
    }
}