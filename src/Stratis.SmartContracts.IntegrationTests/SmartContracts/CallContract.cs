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

    public bool CallOther(Address address)
    {

        ITransferResult result = Call(address, 100, "IncrementCount");

        return result.Success;
    }

    public bool Tester(Address address)
    {
        Test = "Not Initial!";
        ITransferResult result = Call(address, 0, "Callback");
        return (bool)result.ReturnValue;
    }

    public bool Asserter()
    {
        return Test == "Not Initial!";
    }
}
