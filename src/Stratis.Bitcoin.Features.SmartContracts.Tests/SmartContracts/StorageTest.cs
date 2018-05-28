using Stratis.SmartContracts;

public class StorageTest : SmartContract
{
    public StorageTest(ISmartContractState state)
        : base(state)
    {
    }

    public void StoreData()
    {
        this.PersistentState.SetString("TestKey", "TestValue");
    }

    public void GasTest()
    {
        ulong test = 1;
        while (true)
        {
            test++;
            test--;
        }
    }
}