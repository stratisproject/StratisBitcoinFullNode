using System;
using NBitcoin;

namespace Stratis.SmartContracts.Core.Receipts
{
    /// <summary>
    /// Saves receipts after smart contract execution.
    /// </summary>
    public interface ISmartContractReceiptStorage
    {
        /// <summary>
        /// Save the receipt to a permanent location.
        /// </summary>
        void SaveReceipt(ISmartContractTransactionContext txContext, ISmartContractExecutionResult result);

        /// <summary>
        /// Save the receipt to a permanent location.
        /// </summary>
        void SaveReceipt(
            uint256 txHash,
            ulong blockHeight,
            uint160 newContractAddress,
            ulong gasConsumed,
            bool successful,
            Exception exception,
            object returned);

        /// <summary>
        /// Retrieve the saved receipt for a given transaction hash.
        /// </summary>
        SmartContractReceipt GetReceipt(uint256 txHash);
    }
}
