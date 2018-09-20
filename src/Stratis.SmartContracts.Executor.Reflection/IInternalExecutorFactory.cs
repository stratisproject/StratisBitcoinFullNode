namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IInternalExecutorFactory
    {
        IInternalTransactionExecutor Create(IState state);
    }
}