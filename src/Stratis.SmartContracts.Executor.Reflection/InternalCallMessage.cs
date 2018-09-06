﻿using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Represents an internal contract method call message. Occurs when a contract generates a method call to another contract
    /// using its <see cref="SmartContract.Call"/> method.
    /// </summary>
    public class InternalCallMessage : CallMessage
    {
        public InternalCallMessage(uint160 to, uint160 from, ulong amount, Gas gasLimit, MethodCall methodCall)
            : base(to, from, amount, gasLimit, methodCall)
        {
        }
    }
}