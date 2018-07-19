using System;
using Stratis.SmartContracts;

[ToDeploy]
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