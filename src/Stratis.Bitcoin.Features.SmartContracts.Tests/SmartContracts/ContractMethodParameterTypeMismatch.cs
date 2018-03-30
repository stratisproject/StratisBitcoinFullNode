using Stratis.SmartContracts;

public sealed class ContractMethodParameterTypeMismatch : SmartContract
{
    public ContractMethodParameterTypeMismatch(ISmartContractState state)
        : base(state)
    {
    }

    [SmartContractInit]
    public void TestMethod(int orders)
    {
    }
}