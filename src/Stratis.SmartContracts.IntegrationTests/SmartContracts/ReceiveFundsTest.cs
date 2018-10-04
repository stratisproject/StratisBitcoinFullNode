using Stratis.SmartContracts;

[Deploy]
public class ReceiveFundsTest : SmartContract
{
    public ReceiveFundsTest(ISmartContractState smartContractState) : base(smartContractState)
    {
        this.PersistentState.SetUInt64("Balance", this.Balance);
    }

    public void MethodReceiveFunds()
    {
        this.PersistentState.SetUInt64("Balance", this.Balance);
    }

    public void TransferFunds(Address other, ulong amount)
    {
        Transfer(other, amount);
    }

    public override void Receive()
    {
        this.PersistentState.SetUInt64("ReceiveBalance", this.Balance);
    }
}