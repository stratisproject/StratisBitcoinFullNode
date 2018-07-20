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
        public static uint160 GetNewContractAddress(this Transaction transaction)
        {
            return GetContractAddressFromTransactionHash(transaction.GetHash());
        }

        public static uint160 GetContractAddressFromTransactionHash(uint256 hash)
        {
            return new uint160(HashHelper.Keccak256(hash.ToBytes()).Take(20).ToArray());
        }
    }
}