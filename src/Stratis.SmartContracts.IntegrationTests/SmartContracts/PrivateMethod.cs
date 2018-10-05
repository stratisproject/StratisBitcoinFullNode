using Stratis.SmartContracts;

public class PrivateMethod : SmartContract
{
    public PrivateMethod(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    private void CallMe()
    {
        this.PersistentState.SetBool("Called", true);
        this.Log(new Called{From= this.Message.Sender});
    }

    public struct Called
    {
        public Address From;
    }
}