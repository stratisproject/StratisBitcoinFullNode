using System;
using DBreeze.Exceptions;

namespace Stratis.Bitcoin.Utilities
{
    public class DBreezeRetryOptions : RetryOptions
    {
        public DBreezeRetryOptions(RetryStrategyType type = RetryStrategyType.Simple)
            : base(1, TimeSpan.FromMilliseconds(100), type, typeof(TableNotOperableException))
        {
        }

        public static RetryOptions Default => new DBreezeRetryOptions();
    }
}