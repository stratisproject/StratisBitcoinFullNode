using Stratis.SmartContracts;

public class StorageTest : SmartContract
{
    public StorageTest(ISmartContractState state)
        : base(state)
    {
    }

    public bool NoParamsTest()
    {
        return true;
    }

    public int OneParamTest(int orders)
    {
        return orders;
    }
}