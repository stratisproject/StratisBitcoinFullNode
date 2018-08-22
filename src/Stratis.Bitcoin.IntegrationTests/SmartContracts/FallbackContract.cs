using Stratis.SmartContracts;

public class FallbackContract : SmartContract
{
    public FallbackContract(ISmartContractState state) : base(state) { }

    public void SendFunds(Address destination, ulong amount)
    {
        Transfer(destination, amount);
    }

    public override void Fallback()
    {
        this.PersistentState.SetBool("FallbackInvoked", true);
        this.PersistentState.SetUInt64("FallbackReceived", this.Message.Value);
    }
}