using Stratis.SmartContracts;

[Deploy]
public class InterContract2 : SmartContract
{
    public InterContract2(ISmartContractState state) : base(state) { }

    public int ContractTransfer(string addressString)
    {
        ITransferResult result = TransferFunds(new Address(addressString), 100, new TransferFundsToContract
        {
            ContractMethodName = "ReturnInt"
        });

        return (int) result.ReturnValue;
    }

    public bool ContractTransferWithFail(string addressString)
    {
        ITransferResult result = TransferFunds(new Address(addressString), 100, new TransferFundsToContract
        {
            ContractMethodName = "ThrowException"
        });

        return result.Success;
    }
}
