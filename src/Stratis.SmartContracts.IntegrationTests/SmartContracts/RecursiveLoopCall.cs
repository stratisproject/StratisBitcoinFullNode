using Stratis.SmartContracts;

public class RecursiveLoopCall : SmartContract
{
    public RecursiveLoopCall(ISmartContractState state) : base(state)
    {
    }

    public void Call()
    {
        Assert(Call(this.Address, 0, nameof(this.Call)).Success);
    }
}
