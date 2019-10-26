using Stratis.SmartContracts;

public class NestedCallsReceiver : SmartContract
{
    public NestedCallsReceiver(ISmartContractState state) : base(state)
    {
    }

    public int Call1()
    {
        int result = (int)Call(this.Message.Sender, this.Balance / 2, "Call2").ReturnValue;
        return result;
    }
}
