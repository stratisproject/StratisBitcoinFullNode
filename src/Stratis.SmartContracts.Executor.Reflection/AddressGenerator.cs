using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class AddressGenerator : IAddressGenerator
    {
        public uint160 GenerateAddress(uint256 seed, ulong nonce)
        {
            byte[] toHash = seed.ToBytes().Concat(new uint256(nonce).ToBytes()).ToArray();
            return new uint160(HashHelper.Keccak256(toHash).Take(20).ToArray());
        }
    }
}