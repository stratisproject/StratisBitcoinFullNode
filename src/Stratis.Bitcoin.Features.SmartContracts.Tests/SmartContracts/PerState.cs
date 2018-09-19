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
}
