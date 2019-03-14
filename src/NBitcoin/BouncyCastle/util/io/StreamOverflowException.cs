using System;
using System.IO;

namespace Stratis.Bitcoin.NBitcoin.BouncyCastle.Utilities.IO
{
    internal class StreamOverflowException
        : IOException
    {
        public StreamOverflowException()
            : base()
        {
        }

        public StreamOverflowException(
            string message)
            : base(message)
        {
        }

        public StreamOverflowException(
            string message,
            Exception exception)
            : base(message, exception)
        {
        }
    }
}
