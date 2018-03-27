using Stratis.SmartContracts;

public sealed class ContractMethodParameterTypeMismatch : SmartContract
{
    public ContractMethodParameterTypeMismatch(ISmartContractState state)
        : base(state)
    {
    }

    public void TestMethod(int orders)
    {
    }
}