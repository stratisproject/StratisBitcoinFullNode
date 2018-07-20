using System;
using Stratis.SmartContracts;

public sealed class ThrowExceptionContract : SmartContract
{
    public ThrowExceptionContract(ISmartContractState state)
        : base(state)
    {
    }

    public void ThrowException()
    {
        throw new Exception();
    }
}