using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Core
{
    public interface ISmartContractExecutorFactory
    {
        ISmartContractExecutor CreateExecutor(
            IContractStateRoot stateRepository,
            ISmartContractTransactionContext transactionContext);
    }
}
