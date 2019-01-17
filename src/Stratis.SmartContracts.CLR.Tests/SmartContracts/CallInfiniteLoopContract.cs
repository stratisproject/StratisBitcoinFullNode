using Stratis.SmartContracts;

[Deploy]
public class CallInfiniteLoopContract : SmartContract
{
    public CallInfiniteLoopContract(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public bool CallInfiniteLoop(Address address)
    {
        ITransferResult result = Call(address, 100, "Loop", null, 10_000);

        return result.Success;
    }
}
