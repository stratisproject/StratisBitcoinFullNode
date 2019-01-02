using NBitcoin;

namespace Stratis.SmartContracts.CLR
{
    public interface IPersistenceStrategy
    {
        bool ContractExists(uint160 address);
        byte[] FetchBytes(uint160 address, byte[] key);
        void StoreBytes(uint160 address, byte[] key, byte[] value);
    }
}