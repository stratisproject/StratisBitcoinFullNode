using Stratis.SmartContracts;

[Deploy]
public class CountContract : SmartContract
{
    public CountContract(ISmartContractState state) : base(state) { }

    public int Count
    {
        get
        {
            return PersistentState.GetInt32("Count");
        }
        set
        {
            PersistentState.SetInt32("Count", value);
        }
    }

    public bool SaveWorked {
        get
        {
            return PersistentState.GetBool("SaveWorked");
        }
        set
        {
            PersistentState.SetBool("SaveWorked", value);
        }
    }

    public void IncrementCount()
    {
        this.Count++;
    }

    public bool Callback()
    {
        ITransferResult result = Call(new Address(Message.Sender), 0, "Asserter");
        SaveWorked = (bool)result.ReturnValue;
        return (bool)result.ReturnValue;
    }
}