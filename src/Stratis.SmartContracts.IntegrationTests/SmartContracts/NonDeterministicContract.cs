
using Stratis.SmartContracts;

public class NonDeterministicContract : SmartContract
{
    public NonDeterministicContract(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public float Test()
    {
        float notAllowed = 0.7F;
        return notAllowed;
    }
}
