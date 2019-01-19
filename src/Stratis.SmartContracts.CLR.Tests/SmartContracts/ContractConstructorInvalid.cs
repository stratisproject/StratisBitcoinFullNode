using Stratis.SmartContracts;

[Deploy]
public sealed class ContractConstructorInvalid : SmartContract
{
    public ContractConstructorInvalid(ISmartContractState state)
        : base(state)
    {
        Assert(false);
    }
}