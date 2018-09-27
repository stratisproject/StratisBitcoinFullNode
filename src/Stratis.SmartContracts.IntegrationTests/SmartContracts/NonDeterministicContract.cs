
using Stratis.SmartContracts;

public class NonDeterministicContract : SmartContract
{
    public NonDeterministicContract(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public void Test()
    {
        float notAllowed = 0.7F;
    }
}
