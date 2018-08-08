using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public static class NewContractAddressExtension
    {
        /// <summary>
        /// Shortcut to get the address of the contract that was deployed with this transaction.
        /// </summary>
        public static uint160 GetDeployedContractAddress(this SmartContractCarrier carrier)
        {
            return carrier.GetNewContractAddress(0);
        }

        /// <summary>
        /// Get the address for a newly deployed contract.
        /// </summary>
        /// <param name="nonce">Number to be appended to transactionId before hashing to get the new address.</param>
        public static uint160 GetNewContractAddress(this SmartContractCarrier carrier, ulong nonce)
        {
            return Core.NewContractAddressExtension.GetContractAddressFromTransactionHash(carrier.TransactionHash, nonce);
        }
    }
}
