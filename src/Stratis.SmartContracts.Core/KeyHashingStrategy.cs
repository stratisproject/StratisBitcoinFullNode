using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.Core
{
    public class KeyHashingStrategy : IKeyEncodingStrategy
    {
        public static KeyHashingStrategy Default = new KeyHashingStrategy();

        public byte[] GetBytes(byte[] key)
        {
            return HashHelper.Keccak256(key);
        }
    }
}