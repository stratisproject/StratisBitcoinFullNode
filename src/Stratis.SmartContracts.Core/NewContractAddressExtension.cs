using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core
{
    public static class NewContractAddressExtension
    {
        /// <summary>
        /// Shortcut to get the address of the contract that was deployed with this transaction.
        /// </summary>
        public static uint160 GetDeployedContractAddress(this Transaction transaction)
        {
            return transaction.GetNewContractAddress(0);
        }

        /// <summary>
        /// Get the address for a newly deployed contract.
        /// </summary>
        /// <param name="nonce">Number to be appended to transactionId before hashing to get the new address.</param>
        public static uint160 GetNewContractAddress(this Transaction transaction, ulong nonce)
        {
            return GetContractAddressFromTransactionHash(transaction.GetHash(), nonce);
        }

        /// <summary>
        /// Get the address for a newly deployed contract.
        /// </summary>
        /// <param name="hash">TransactionId of currently executing transaction.</param>
        /// <param name="nonce">Number to be appended to TransactionId before hashing to get the new address.</param>
        public static uint160 GetContractAddressFromTransactionHash(uint256 hash, ulong nonce)
        {
            byte[] toHash = hash.ToBytes().Concat(new uint256(nonce).ToBytes()).ToArray();
            return new uint160(HashHelper.Keccak256(toHash).Take(20).ToArray());
        }
    }
}