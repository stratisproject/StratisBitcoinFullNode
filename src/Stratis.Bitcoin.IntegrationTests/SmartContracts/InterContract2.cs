using Stratis.SmartContracts;

public class InterContract2 : SmartContract
{
    public InterContract2(SmartContractState state) : base(state) { }

    public int ContractTransfer()
    {
        TransferResult result = Transfer(new Address("bd5e68f7b70bbf4b7b4c3655b9736f582676e7e8"), 100, new TransactionDetails
        {
            ContractMethodName = "ReturnInt",
            ContractTypeName = "InterContract1"
        });

        return (int) result.ReturnValue;
    }
}
