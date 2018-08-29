using Stratis.SmartContracts;

[Deploy]
public class InterContract2 : SmartContract
{
    public InterContract2(ISmartContractState state) : base(state) { }

    public void ContractTransfer(string addressString)
    {
        ITransferResult result = Call(new Address(addressString), 100, "ReturnInt");
    }

    public bool ContractTransferWithFail(string addressString)
    {
        ITransferResult result = Call(new Address(addressString), 100, "ThrowException");

        return result.Success;
    }
}
