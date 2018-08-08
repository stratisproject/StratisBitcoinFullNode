using System.Linq;
using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public class AddressGenerator : IAddressGenerator
    {
        public uint160 GenerateAddress(ITransactionContext context)
        {
            uint256 hash = context.TransactionHash;
            ulong nonce = context.GetNonceAndIncrement();
            byte[] toHash = hash.ToBytes().Concat(new uint256(nonce).ToBytes()).ToArray();
            return new uint160(HashHelper.Keccak256(toHash).Take(20).ToArray());
        }
    }
}