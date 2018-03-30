using Stratis.SmartContracts;

public sealed class ContractInvalidParameterCount : SmartContract
{
    public ContractInvalidParameterCount(ISmartContractState state)
        : base(state)
    {
    }

    [SmartContractInit]
    public void TestMethod(int orders, bool canOrder)
    {
    }
}