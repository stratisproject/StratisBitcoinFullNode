using Stratis.Bitcoin.NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    public interface IAddressGenerator
    {
        uint160 GenerateAddress(uint256 seed, ulong nonce);
    }
}