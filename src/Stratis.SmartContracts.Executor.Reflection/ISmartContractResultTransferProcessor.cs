using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.Executor.Reflection
{
    /// <summary>
    /// Handles value transfers as a result of smart contract execution.
    /// </summary>
    public interface ISmartContractResultTransferProcessor
    {
        /// <summary>
        /// Returns a single Transaction which accounts for value transfers that occurred during contract execution.
        /// </summary>
        Transaction Process(IContractState stateSnapshot,
            uint160 contractAddress,
            ISmartContractTransactionContext transactionContext,
            IReadOnlyList<TransferInfo> internalTransfers,
            bool reversionRequired);
    }
}
