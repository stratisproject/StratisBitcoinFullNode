using Stratis.SmartContracts;

[Deploy]
public class NonceTest : SmartContract
{
    public NonceTest(ISmartContractState smartContractState) : base(smartContractState)
    {
        Create<CreateSuccess>(1);
        Create<CreateFail>(1);
        Create<CreateSuccess>(1);
    }
}


public class CreateSuccess : SmartContract
{
    public CreateSuccess(ISmartContractState smartContractState) : base(smartContractState)
    {
    }
}

public class CreateFail : SmartContract
{
    public CreateFail(ISmartContractState smartContractState) : base(smartContractState)
    {
        Assert(false);
    }
}