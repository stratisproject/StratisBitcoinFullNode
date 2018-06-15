using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface ISmartContractResultTransferProcessor
    {
        void Process(
            SmartContractCarrier carrier,
            ISmartContractExecutionResult result,
            IContractStateRepository stateSnapshot,
            ISmartContractTransactionContext transactionContext);
    }
}
