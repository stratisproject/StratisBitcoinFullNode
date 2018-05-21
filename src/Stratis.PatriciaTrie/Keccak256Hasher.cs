using HashLib;

namespace Stratis.Patricia
{
    public class Keccak256Hasher : IHasher
    {
        private static readonly IHash Keccak = HashFactory.Crypto.SHA3.CreateKeccak256();

        /// <inheritdoc />
        public byte[] Hash(byte[] input)
        {
            return Keccak.ComputeBytes(input).GetBytes();
        }
    }
}
