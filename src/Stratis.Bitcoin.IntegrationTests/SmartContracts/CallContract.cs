using Stratis.SmartContracts;

[Deploy]
public class CallContract : SmartContract
{
    public CallContract(ISmartContractState state) : base(state)
    {
        Test = "Initial";
    }

    public string Test
    {
        get
        {
            return PersistentState.GetString("Test");
        }
        set
        {
            PersistentState.SetString("Test", value);
        }
    }

    public bool CallOther(string addressString)
    {

        ITransferResult result = Call(new Address(addressString), 100, "IncrementCount");

        return result.Success;
    }

    public bool Tester(string addressString)
    {
        Test = "Not Initial!";
        ITransferResult result = Call(new Address(addressString), 0, "Callback");
        return (bool)result.ReturnValue;
    }

    public bool Asserter()
    {
        return Test == "Not Initial!";
    }
}
