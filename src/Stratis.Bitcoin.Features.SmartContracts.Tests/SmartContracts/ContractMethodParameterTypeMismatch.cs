using Stratis.SmartContracts;

public sealed class ContractMethodParameterTypeMismatch : SmartContract
{
    public ContractMethodParameterTypeMismatch(ISmartContractState state, int orders)
        : base(state)
    {
    }
}