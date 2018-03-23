using HashLib;
using Nethereum.RLP;

namespace Stratis.SmartContracts.Core.Hashing
{
    /// <summary>
    /// Utilities to aid with hashing of data.
    /// </summary>
    public static class HashHelper
    {
        public static readonly byte[] EmptyByteArray = new byte[0];
        public static readonly byte[] EmptyDataHash = Keccak256(EmptyByteArray);
        public static readonly byte[] EmptyElementRlp = RLP.EncodeElement(EmptyByteArray);
        public static readonly byte[] EmptyTrieHash = Keccak256(EmptyElementRlp);
        private static readonly IHash Keccak = HashFactory.Crypto.SHA3.CreateKeccak256();

        public static byte[] Keccak256(byte[] input)
        {
            return Keccak.ComputeBytes(input).GetBytes();
        }
    }

    public class InternalHashHelper : IInternalHashHelper
    {
        public byte[] Keccak256(byte[] toHash)
        {
            return HashHelper.Keccak256(toHash);
        }
    }
}