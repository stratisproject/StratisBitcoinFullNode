using Stratis.SmartContracts;

[Deploy]
public class MultipleNestedCalls : SmartContract
{
    public MultipleNestedCalls(ISmartContractState smartContractState) : base(smartContractState)
    {
        Create<Caller>(this.Balance);
    }

    public void CalledInsideConstructor()
    {
        this.PersistentState.SetAddress("Caller", this.Message.Sender);
    }
}

public class Caller : SmartContract
{
    public Caller(ISmartContractState smartContractState) : base(smartContractState)
    {
        Call(this.Message.Sender, this.Balance / 2, "CalledInsideConstructor");
    }
}

