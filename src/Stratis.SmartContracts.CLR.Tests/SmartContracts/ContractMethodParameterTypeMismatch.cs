using Stratis.SmartContracts;

[Deploy]
public sealed class ContractMethodParameterTypeMismatch : SmartContract
{
    public ContractMethodParameterTypeMismatch(ISmartContractState state, int orders)
        : base(state)
    {
    }
}