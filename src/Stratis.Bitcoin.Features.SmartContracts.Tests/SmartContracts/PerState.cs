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

    public int GetBalance(string name)
    {
        return PersistentState.GetInt32($"Balance[{name}]");
    }

    public void SetAllowed(string name1, string name2, bool val)
    {
        PersistentState.SetBool($"Allowed[{name1}][{name2}]", val);
    }

    public bool GetAllowed(string name1, string name2)
    {
        return PersistentState.GetBool($"Allowed[{name1}][{name2}]");
    }

    public string GetBuyer(int index)
    {
        return PersistentState.GetString($"Buyers[{index}]");
    }

    public void AddBuyer(string buyer)
    {
        int currentCount = PersistentState.GetInt32("Buyers.Count");
        PersistentState.SetString($"Buyers[{currentCount}]", buyer);
        PersistentState.SetInt32("Buyers.Count", currentCount + 1);
    }

    public string[] GetAllBuyers()
    {
        int count = PersistentState.GetInt32("Buyers.Count");
        string[] allBuyers = new string[count];
        for(int i =0; i< count; i++)
        {
            allBuyers[i] = GetBuyer(i);
        }
        return allBuyers;
    }
}
