using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.CLR.Local;

namespace Stratis.SmartContracts.CLR
{
    public interface ILocalExecutor
    {
        ILocalExecutionResult Execute(IContractTransactionContext transactionContext);
    }
}
