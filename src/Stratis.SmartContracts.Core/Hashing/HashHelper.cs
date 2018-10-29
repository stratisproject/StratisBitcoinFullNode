using HashLib;

namespace Stratis.SmartContracts.Core.Hashing
{
    /// <summary>
    /// Utilities to aid with hashing of data.
    /// </summary>
    public static class HashHelper
    {
        private static readonly IHash Keccak = HashFactory.Crypto.SHA3.CreateKeccak256();

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