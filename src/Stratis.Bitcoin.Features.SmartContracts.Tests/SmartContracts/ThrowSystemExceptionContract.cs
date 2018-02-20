using System;
using Stratis.SmartContracts;

public sealed class ThrowSystemExceptionContract : SmartContract
{
    public ThrowSystemExceptionContract(SmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}