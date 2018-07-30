using Stratis.SmartContracts;

[Deploy]
public sealed class ContractInvalidParameterCount : SmartContract
{
    public ContractInvalidParameterCount(ISmartContractState state, int orders, bool canOrder)
        : base(state)
    {
    }    
}