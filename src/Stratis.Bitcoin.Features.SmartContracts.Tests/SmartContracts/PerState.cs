using Stratis.SmartContracts;


public class PerState : SmartContract
{

    public int Auto { get; set; }

    public int Manual
    {
        get
        {
            return this.PersistentState.GetInt32("Manual");
        }
        set
        {
            this.PersistentState.SetInt32("Manual", value);
        }
    }

    public PerState(ISmartContractState smartContractState) : base(smartContractState)
    {
    }


    public void SetBalance(string name, int val)
    {
        PersistentState.SetInt32($"Balance[{name}]", val);
    }

    public void SetBalance2d(string name1, string name2, int val)
    {
        PersistentState.SetInt32($"Balance[{name1}][{name2}]", val);
    }


    public void AddBuyer()
    {
        // add to a list

        // increment a count

        // need to be able to loop over
    }







}
