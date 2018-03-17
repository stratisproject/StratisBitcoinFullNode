using System.Linq;
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

        public static byte[] Keccak256(byte[] input)
        {
            return HashFactory.Crypto.SHA3.CreateKeccak256().ComputeBytes(input).GetBytes();
        }

        /// <summary>
        /// TODO: Concrete rules around byte size here OR remove if we're not even using it
        /// </summary>
        /// <param name="address"></param>
        /// <param name="nonce"></param>
        public static byte[] NewContractAddress(byte[] address, byte[] nonce)
        {
            return Keccak256(address.Concat(nonce).ToArray()).Skip(12).ToArray();
        }
    }
}