namespace Stratis.SmartContracts.Core
{
    public interface IContractExecutor
    {
        IContractExecutionResult Execute(IContractTransactionContext transactionContext);
    }
}
