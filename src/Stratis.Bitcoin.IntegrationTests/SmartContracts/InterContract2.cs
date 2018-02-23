using Stratis.SmartContracts;

public class InterContract2 : SmartContract
{
    public InterContract2(SmartContractState state) : base(state) { }

    public int ContractTransfer(string addressString)
    {
        TransferResult result = Transfer(new Address(addressString), 100, new TransactionDetails
        {
            ContractMethodName = "ReturnInt",
            ContractTypeName = "InterContract1"
        });

        return (int) result.ReturnValue;
    }

    public bool ContractTransferWithFail(string addressString)
    {
        TransferResult result = Transfer(new Address(addressString), 100, new TransactionDetails
        {
            ContractMethodName = "ThrowException",
            ContractTypeName = "InterContract1"
        });

        return result.Success;
    }
}
