using Stratis.SmartContracts;

[Deploy]
public class CallInfiniteLoopContract : SmartContract
{
    public CallInfiniteLoopContract(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public bool CallInfiniteLoop(string addressString)
    {
        ITransferResult result = Call(new Address(addressString), 100, "Loop", null);

        return result.Success;
    }
}
