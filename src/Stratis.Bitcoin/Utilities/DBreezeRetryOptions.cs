using System;
using DBreeze.Exceptions;

namespace Stratis.Bitcoin.Utilities
{
    public class DBreezeRetryOptions : RetryOptions
    {
        public DBreezeRetryOptions()
            : base(1, TimeSpan.FromMilliseconds(100), typeof(TableNotOperableException))
        {
        }

        public static RetryOptions Default => new DBreezeRetryOptions();
    }
}