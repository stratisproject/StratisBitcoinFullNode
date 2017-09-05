using NBitcoin.RPC;
using System;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCServerException : Exception
    {
        public RPCServerException(RPCErrorCode errorCode, string message) : base(message)
        {
            this.ErrorCode = errorCode;
        }

        public RPCErrorCode ErrorCode
        {
            get; set;
        }
    }
}
