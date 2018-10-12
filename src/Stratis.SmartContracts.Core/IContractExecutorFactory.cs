using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    public interface IContractExecutorFactory
    {
        IContractExecutor CreateExecutor(
            IStateRepositoryRoot stateRepository,
            IContractTransactionContext transactionContext);
    }
}
