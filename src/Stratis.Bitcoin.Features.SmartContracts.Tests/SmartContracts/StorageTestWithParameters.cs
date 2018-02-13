using Stratis.SmartContracts;

public sealed class StorageTestWithParameters : CompiledSmartContract
{
    [SmartContractInit]
    public void Init()
    {
    }

    public void StoreData(int orders)
    {
        PersistentState.SetObject("orders", orders);
    }
}