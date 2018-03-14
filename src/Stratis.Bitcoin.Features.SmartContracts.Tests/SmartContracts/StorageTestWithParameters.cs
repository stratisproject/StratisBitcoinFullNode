using Stratis.SmartContracts;

public sealed class StorageTestWithParameters : SmartContract
{
    public StorageTestWithParameters(ISmartContractState state)
        : base(state)
    {
    }

    [SmartContractInit]
    public void Init()
    {
    }

    public void StoreData(int orders)
    {
        this.PersistentState.SetObject("orders", orders);
    }
}