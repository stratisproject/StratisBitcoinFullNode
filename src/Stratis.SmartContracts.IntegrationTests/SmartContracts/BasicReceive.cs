using Stratis.SmartContracts;

public class BasicReceive : SmartContract
{
    public const string ReceiveKey = "Received";

    public BasicReceive(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public override void Receive()
    {
        this.PersistentState.SetBool(ReceiveKey, true);
        this.Log(new Received{Sender = this.Message.Sender});
    }

    public struct Received
    {
        public Address Sender;
    }
}
