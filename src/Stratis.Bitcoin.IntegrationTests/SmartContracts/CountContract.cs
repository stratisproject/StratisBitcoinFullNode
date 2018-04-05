using Stratis.SmartContracts;

public class CountContract : SmartContract
{
    public CountContract(ISmartContractState state) : base(state) { }

    public int Count
    {
        get
        {
            return PersistentState.GetObject<int>("Count");
        }
        set
        {
            PersistentState.SetObject<int>("Count", value);
        }
    }

    public bool SaveWorked {
        get
        {
            return PersistentState.GetObject<bool>("SaveWorked");
        }
        set
        {
            PersistentState.SetObject("SaveWorked", value);
        }
    }

    public void IncrementCount()
    {
        this.Count++;
    }

    public bool Callback()
    {
        ITransferResult result = TransferFunds(new Address(Message.Sender), 0, new TransferFundsToContract
        {
            ContractMethodName = "Asserter",
            ContractTypeName = "CallContract"
        });
        SaveWorked = (bool)result.ReturnValue;
        return (bool)result.ReturnValue;
    }
}