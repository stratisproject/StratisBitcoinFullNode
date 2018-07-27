using Stratis.SmartContracts;

[Deploy]
public class CallInfiniteLoopContract : SmartContract
{
    public CallInfiniteLoopContract(ISmartContractState smartContractState) : base(smartContractState)
    {
    }

    public bool CallInfiniteLoop(string addressString)
    {
        ITransferResult result = TransferFunds(new Address(addressString), 100, new TransferFundsToContract
        {
            ContractMethodName = "Loop"
        });

        return result.Success;
    }
}
