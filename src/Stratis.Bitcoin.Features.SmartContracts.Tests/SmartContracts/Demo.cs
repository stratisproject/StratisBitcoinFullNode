using Stratis.SmartContracts;

public class Demo : SmartContract
{
    public Demo(ISmartContractState state) : base(state)
    {
        Counter = 12345;
        TestSave = "Hello, smart contract world!";
    }

    public int Counter
    {
        get
        {
            return PersistentState.GetObject<int>("Counter");
        }
        set
        {
            PersistentState.SetObject<int>("Counter", value);
        }
    }

    public string TestSave
    {
        get
        {
            return PersistentState.GetObject<string>("TestSave");
        }
        set
        {
            PersistentState.SetObject<string>("TestSave", value);
        }
    }

    public void Increment()
    {
        Counter++;
    }
}
