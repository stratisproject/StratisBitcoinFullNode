using Stratis.SmartContracts;

public sealed class StorageTestWithParameters : SmartContract
{
    public StorageTestWithParameters(ISmartContractState state)
        : base(state)
    {
    }

    public void StoreData(int orders)
    {
        this.PersistentState.SetObject("orders", orders);
    }
}