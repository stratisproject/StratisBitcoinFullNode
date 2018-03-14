namespace Stratis.SmartContracts
{
    public interface IInternalTransactionExecutor
    {
        ITransferResult Transfer(ISmartContractState state, Address addressTo, ulong amount, TransactionDetails transactionDetails);
    }
}