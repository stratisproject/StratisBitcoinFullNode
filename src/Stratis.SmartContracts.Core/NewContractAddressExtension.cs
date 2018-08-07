using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core
{
    public static class NewContractAddressExtension
    {
        /// <summary>
        /// Get the address for a newly deployed contract.
        /// </summary>
        /// <param name="transaction"></param>
        public static uint160 GetNewContractAddress(this Transaction transaction, ulong nonce)
        {
            return GetContractAddressFromTransactionHash(transaction.GetHash(), nonce);
        }

        public static uint160 GetContractAddressFromTransactionHash(uint256 hash, ulong nonce)
        {
            byte[] toHash = hash.ToBytes().Concat(new uint256(nonce).ToBytes()).ToArray();
            return new uint160(HashHelper.Keccak256(toHash).Take(20).ToArray());
        }
    }
}