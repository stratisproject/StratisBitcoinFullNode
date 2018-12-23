using Stratis.SmartContracts;

[Deploy]
public class ConstructorLoop : SmartContract
{
    public ConstructorLoop(ISmartContractState state) : base(state)
    {
        // This will only allow contract creation to happen if the inner call does run out of gas.
        Assert(!Create<Other>().Success);
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