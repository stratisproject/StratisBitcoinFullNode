using NBitcoin;

namespace Stratis.SmartContracts.Executor.Reflection
{
    public interface IPersistenceStrategy
    {
        byte[] FetchBytes(uint160 address, byte[] key);
        void StoreBytes(uint160 address, byte[] key, byte[] value);
    }
}