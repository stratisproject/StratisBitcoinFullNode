using Stratis.SmartContracts;

public sealed class ContractMethodParametersUnresolved : SmartContract
{
    public ContractMethodParametersUnresolved(ISmartContractState state)
        : base(state)
    {
    }

    public void TestMethod(int orders, bool canOrder)
    {
    }
}