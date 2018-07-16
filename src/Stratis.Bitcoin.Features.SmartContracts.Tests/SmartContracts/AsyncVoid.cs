using Stratis.SmartContracts;

public class AsyncVoid : SmartContract
{
    public AsyncVoid(ISmartContractState smartContractState)
        : base(smartContractState)
    {
    }

    public AsyncVoid(ISmartContractState smartContractState, uint param)
        : base(smartContractState)
    {
    }

    public async void Test()
    {
    }
}