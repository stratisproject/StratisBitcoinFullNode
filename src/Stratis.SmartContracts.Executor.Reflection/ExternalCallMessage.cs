﻿using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents an external contract method call message. Occurs when a transaction is received that contains contract method call data.
    /// </summary>
    public class ExternalCallMessage : CallMessage
    {
        public ExternalCallMessage(uint160 to, uint160 from, ulong amount, Gas gasLimit, MethodCall methodCall) 
            : base(to, from, amount, gasLimit, methodCall)
        {
        }
    }
}