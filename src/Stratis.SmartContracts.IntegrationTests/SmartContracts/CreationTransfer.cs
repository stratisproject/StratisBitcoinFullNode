using Stratis.SmartContracts;

[Deploy]
public class CreationTransfer : SmartContract
{
    public CreationTransfer(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void CreateAnotherContract()
    {
        Create<AnotherContract>(this.Balance);
    }
}

public class AnotherContract : SmartContract
{
    public AnotherContract(ISmartContractState smartContractState) : base(smartContractState)
    {
    }
}
