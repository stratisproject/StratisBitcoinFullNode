using Stratis.SmartContracts;

[Deploy]
public class ConstructorLoop : SmartContract
{
    public ConstructorLoop(ISmartContractState state) : base(state)
    {
        Create<Other>();
    }
}

public class Other : SmartContract
{
    public Other(ISmartContractState state) : base(state)
    {
        var i = 1;
        while (true)
        {
            PersistentState.SetInt32("i", i++);
        }
    }
}