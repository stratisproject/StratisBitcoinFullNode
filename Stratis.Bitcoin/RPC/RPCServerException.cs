using NBitcoin.RPC;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.RPC
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
