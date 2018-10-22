using Stratis.SmartContracts;

public class ClearStorage : SmartContract
{
    public const string KeyToClear = "Key";

    public ClearStorage(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void ClearKey()
    {
        this.PersistentState.Clear(KeyToClear);
    }
}
