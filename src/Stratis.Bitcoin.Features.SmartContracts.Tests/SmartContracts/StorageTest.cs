using Stratis.SmartContracts;

public class StorageTest : SmartContract
{
    public StorageTest(SmartContractState state)
        : base(state)
    {
    }

    [SmartContractInit]
    public void Init()
    {
    }

    public void StoreData()
    {
        this.PersistentState.SetObject("TestKey", "TestValue");
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