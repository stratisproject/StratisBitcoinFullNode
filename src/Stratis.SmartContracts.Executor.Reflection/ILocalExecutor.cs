using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection.Local;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ILocalExecutor
    {
        ILocalExecutionResult Execute(IContractTransactionContext transactionContext);
    }
}
