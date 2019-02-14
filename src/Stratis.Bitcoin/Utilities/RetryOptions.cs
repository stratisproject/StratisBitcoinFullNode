using System;
using System.Linq;

namespace Stratis.Bitcoin.Utilities
{
    public class RetryOptions
    {
        public RetryOptions()
        {
            this.RetryCount = 1;
            this.Delay = TimeSpan.FromMilliseconds(100);
            this.ExceptionTypes = new[] { typeof(Exception) };
        }

        public RetryOptions(short retryCount, TimeSpan delay, params Type[] exceptionTypeTypes)
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

            if (exceptionTypeTypes.Length == 0)
            {
                this.ExceptionTypes = new[] { typeof(Exception) };
            }

            this.RetryCount = retryCount;
            this.Delay = delay;
            this.ExceptionTypes = exceptionTypeTypes;
        }

        public static RetryOptions Default => new RetryOptions();

        public short RetryCount { get; }

        public Type[] ExceptionTypes { get; }

        public TimeSpan Delay { get; }

        public override string ToString()
        {
            return $"Retry count: {this.RetryCount}; Delay: {this.Delay}; Exceptions: {string.Join(", ", this.ExceptionTypes.Select(et => et.FullName))}";
        }
    }
}