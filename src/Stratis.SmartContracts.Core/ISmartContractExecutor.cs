namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractExecutor
    {
        ISmartContractExecutionResult Execute(ISmartContractTransactionContext transactionContext);
    }
}
