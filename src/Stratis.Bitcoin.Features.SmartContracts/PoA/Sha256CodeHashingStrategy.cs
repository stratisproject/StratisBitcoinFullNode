using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    /// <summary>
    /// Hashes the supplied data using SHA256.
    /// </summary>
    public class Sha256CodeHashingStrategy : IContractCodeHashingStrategy
    {
        /// <summary>
        /// Hashes the given bytes using SHA256.
        /// </summary>
        /// <param name="data">The data to hash.</param>
        /// <returns>The SHA256 hash of the data.</returns>
        public byte[] Hash(byte[] data)
        {
            return Hashes.Hash256(data).ToBytes();
        }
    }
}