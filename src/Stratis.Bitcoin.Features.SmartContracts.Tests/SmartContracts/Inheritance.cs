using Stratis.SmartContracts;

public abstract class Inheritance : SmartContract
{
    protected Inheritance(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    protected int SomeMethod()
    {
        return 0;
    }
}

[Deploy]
public class Child : Inheritance
{
    public Child(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public int SomePublicMethod()
    {
        return SomeMethod();
    }
}
