namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IInternalTransactionExecutorFactory
    {
        IInternalTransactionExecutor Create(IState state);
    }
}