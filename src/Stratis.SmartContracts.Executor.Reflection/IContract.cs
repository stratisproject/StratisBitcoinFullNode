using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IContract
    {
        uint160 Address { get; }
        Type Type { get; }
        ISmartContractState State { get; }
        IContractInvocationResult InvokeConstructor(IReadOnlyList<object> parameters);
        IContractInvocationResult Invoke(string methodName, IReadOnlyList<object> parameters);
    }
}