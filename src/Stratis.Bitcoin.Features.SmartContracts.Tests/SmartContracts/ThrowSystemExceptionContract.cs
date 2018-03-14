using System;
using Stratis.SmartContracts;

public sealed class ThrowSystemExceptionContract : SmartContract
{
    public ThrowSystemExceptionContract(ISmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}