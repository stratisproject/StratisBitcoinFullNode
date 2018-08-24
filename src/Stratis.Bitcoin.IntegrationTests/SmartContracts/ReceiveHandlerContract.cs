using Stratis.SmartContracts;

public class ReceiveHandlerContract : SmartContract
{
    public ReceiveHandlerContract(ISmartContractState state) : base(state) { }

    public void SendFunds(Address destination, ulong amount)
    {
        Transfer(destination, amount);
    }

    public override void Receive()
    {
        this.PersistentState.SetBool("ReceiveInvoked", true);
        this.PersistentState.SetUInt64("ReceivedFunds", this.Message.Value);
    }
}