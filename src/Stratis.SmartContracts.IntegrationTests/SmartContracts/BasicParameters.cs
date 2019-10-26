using Stratis.SmartContracts;

public class BasicParameters : SmartContract
{
    public BasicParameters(ISmartContractState smartContractState, ulong number1, ulong number2) : base(smartContractState)
    {
        this.PersistentState.SetBool("Created", true);
        this.Log(new Created { From = this.Message.Sender });
    }

    public struct Created
    {
        public Address From;
    }
}

