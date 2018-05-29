using NBitcoin;
using Stratis.SmartContracts.Core.Backend;

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
        void SaveReceipt(uint256 txHash, ulong blockHeight, ISmartContractExecutionResult executionResult, uint160 contractAddress);

        /// <summary>
        /// Retrieve the saved receipt for a given transaction hash.
        /// </summary>
        SmartContractReceipt GetReceipt(uint256 txHash);
    }
}
