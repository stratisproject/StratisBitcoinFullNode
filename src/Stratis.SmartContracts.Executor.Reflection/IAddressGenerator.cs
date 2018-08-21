using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IAddressGenerator
    {
        uint160 GenerateAddress(uint256 seed, ulong nonce);
    }
}