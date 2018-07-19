using System;
using Stratis.SmartContracts;

[ToDeploy]
public sealed class ContractConstructorInvalid : SmartContract
{
    public ContractConstructorInvalid(ISmartContractState state)
        : base(state)
    {
        throw new Exception("test");
    }
}