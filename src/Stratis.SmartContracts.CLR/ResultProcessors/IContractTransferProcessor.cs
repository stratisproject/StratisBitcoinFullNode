using System.Collections.Generic;
using NBitcoin;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.State.AccountAbstractionLayer;

namespace Stratis.SmartContracts.CLR.ResultProcessors
{
    /// <summary>
    /// Handles value transfers as a result of smart contract execution.
    /// </summary>
    public interface IContractTransferProcessor
    {
        /// <summary>
        /// Returns a single Transaction which accounts for value transfers that occurred during contract execution.
        /// </summary>
        Transaction Process(IStateRepository stateSnapshot,
            uint160 contractAddress,
            IContractTransactionContext transactionContext,
            IReadOnlyList<TransferInfo> internalTransfers,
            bool reversionRequired);
    }
}
