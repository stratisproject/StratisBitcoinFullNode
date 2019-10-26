using Stratis.SmartContracts;

public class NestedCallsStarter : SmartContract
{
    public const string Key = "Key";
    public const int Return = 169;

    public NestedCallsStarter(ISmartContractState state) : base(state)
    {
    }

    public int Start(Address sendTo)
    {
        int result = (int) Call(sendTo, this.Balance / 2, "Call1").ReturnValue;
        PersistentState.SetInt32(Key, result);
        return result;
    }

    public int Call2()
    {
        return Return;
    }
}
