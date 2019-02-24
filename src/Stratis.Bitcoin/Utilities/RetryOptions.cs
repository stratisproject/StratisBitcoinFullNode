using System;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    public enum RetryStrategyType
    {
        Simple = 1,
        Backoff
    }

    public class RetryOptions
    {
        public RetryOptions()
        {
            this.RetryCount = 1;
            this.Delay = TimeSpan.FromMilliseconds(100);
            this.ExceptionTypes = Array.Empty<Type>();
            this.Type = RetryStrategyType.Simple;
        }

        public RetryOptions(short retryCount, TimeSpan delay, RetryStrategyType type = RetryStrategyType.Simple, params Type[] exceptionTypeTypes)
        {
            foreach (Type exceptionType in exceptionTypeTypes)
            {
                if (!typeof(Exception).IsAssignableFrom(exceptionType))
                {
                    throw new ArgumentException($"All exception types must be valid exceptions. {exceptionType} is not an Exception.");
                }
            }

            if (retryCount < 1)
                throw new ArgumentOutOfRangeException(nameof(retryCount), "Retry count cannot be less or equal to 0.");

            this.RetryCount = retryCount;
            this.Delay = delay;
            this.ExceptionTypes = exceptionTypeTypes;
            this.Type = type;
        }

        public static RetryOptions Default => new RetryOptions();

        public RetryStrategyType Type { get; }
        
        public short RetryCount { get; }

        public Type[] ExceptionTypes { get; }

        public TimeSpan Delay { get; }

        public override string ToString()
        {
            return $"Retry count: {this.RetryCount}; Delay: {this.Delay}; Exceptions: {string.Join(", ", this.ExceptionTypes.Select(et => et.FullName))}";
        }
    }
}