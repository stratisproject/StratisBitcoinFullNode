using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Encodes keys by applying a Keccak256 hash, returning 32-bytes.
    /// </summary>
    public class KeyHashingStrategy : IKeyEncodingStrategy
    {
        public static KeyHashingStrategy Default = new KeyHashingStrategy();

        public byte[] GetBytes(byte[] key)
        {
            return HashHelper.Keccak256(key);
        }
    }
}