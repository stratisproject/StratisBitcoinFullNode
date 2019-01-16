using System.Linq;
using NBitcoin;
using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.CLR
{
    public class AddressGenerator : IAddressGenerator
    {
        /// <summary>
        /// Deterministically generates a new contract address using the given seed and nonce.
        /// </summary>
        /// <param name="seed">A seed value from which to generate the address. Typically, the hash of a <see cref="Transaction"/> is used.</param>
        /// <param name="nonce">A value which, when combined with the seed, allows for generation of different addresses from the same seed.</param>
        public uint160 GenerateAddress(uint256 seed, ulong nonce)
        {
            byte[] toHash = seed.ToBytes().Concat(new uint256(nonce).ToBytes()).ToArray();
            return new uint160(HashHelper.Keccak256(toHash).Take(20).ToArray());
        }
    }
}