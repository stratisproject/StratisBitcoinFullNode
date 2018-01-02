namespace Stratis.SmartContracts.Backend
{
    internal interface ITransactionExecutor
    {
        ExecutionResult Execute(TestTransaction transaction);
    }
}
