using Stratis.SmartContracts;

[Deploy]
public class BalanceTest : SmartContract
{
    public BalanceTest(ISmartContractState smartContractState) : base(smartContractState)
    {
        Create<ShouldHave0Balance>(10);
    }
}

public class ShouldHave0Balance : SmartContract
{
    public ShouldHave0Balance(ISmartContractState smartContractState) : base(smartContractState)
    {
        this.PersistentState.SetUInt64("Balance", this.Balance);
    }
}