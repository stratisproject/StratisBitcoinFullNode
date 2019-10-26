using Stratis.SmartContracts;

public class TransferFromConstructor : SmartContract
{
    public TransferFromConstructor(ISmartContractState smartContractState, Address address) : base(smartContractState)
    {
        Transfer(address, this.Balance/2);
    }
}

