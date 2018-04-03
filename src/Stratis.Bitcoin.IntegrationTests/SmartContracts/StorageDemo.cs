using Stratis.SmartContracts;

public class StorageDemo : SmartContract
{
    public StorageDemo(ISmartContractState state) : base(state)
    {
        Counter = 12345;
        TestSave = "Hello, smart contract world!";
        Owner = Message.Sender;
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

    public Address Owner
    {
        get
        {
            return PersistentState.GetObject<Address>("Owner");
        }
        set
        {
            PersistentState.SetObject<Address>("Owner", value);
        }
    }

    public void Increment()
    {
        Counter++;
    }
}
