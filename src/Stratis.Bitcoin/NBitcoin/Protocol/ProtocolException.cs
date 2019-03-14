using System;

namespace Stratis.Bitcoin.NBitcoin.Protocol
{
    public class ProtocolException : Exception
    {
        public ProtocolException(string message)
            : base(message)
        {
        }
    }
}