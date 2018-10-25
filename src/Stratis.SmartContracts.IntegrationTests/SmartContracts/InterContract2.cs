using Stratis.SmartContracts;

[Deploy]
public class InterContract2 : SmartContract
{
    public InterContract2(ISmartContractState state) : base(state) { }

    public void ContractTransfer(Address address)
    {
        ITransferResult result = Call(address, 100, "ReturnInt");
    }

    public bool ContractTransferWithFail(Address address)
    {
        ITransferResult result = Call(address, 100, "ThrowException");

        return result.Success;
    }
}
