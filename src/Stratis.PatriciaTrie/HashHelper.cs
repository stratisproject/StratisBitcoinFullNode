using HashLib;
using Nethereum.RLP;

namespace Stratis.PatriciaTrie
{
    internal static class HashHelper
    {
        private static readonly IHash Keccak = HashFactory.Crypto.SHA3.CreateKeccak256();
        public static readonly byte[] EmptyByteArray = new byte[0];
        public static readonly byte[] EmptyDataHash = Keccak256(EmptyByteArray);
        public static readonly byte[] EmptyElementRlp = RLP.EncodeElement(EmptyByteArray);
        public static readonly byte[] EmptyTrieHash = Keccak256(EmptyElementRlp);
        /// <summary>
        /// Returns a 32-byte Keccak256 hash of the given bytes.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] Keccak256(byte[] input)
        {
            return Keccak.ComputeBytes(input).GetBytes();
        }
    }
}
