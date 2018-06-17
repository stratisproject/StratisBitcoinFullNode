using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Handles value transfers as a result of smart contract execution.
    /// </summary>
    public interface ISmartContractResultTransferProcessor
    {
        /// <summary>
        /// Alters an ISmartContractExecutionResult to account for value transfers after smart contract execution.
        /// </summary>
        void Process(
            SmartContractCarrier carrier,
            ISmartContractExecutionResult result,
            IContractStateRepository stateSnapshot,
            ISmartContractTransactionContext transactionContext);
    }
}
