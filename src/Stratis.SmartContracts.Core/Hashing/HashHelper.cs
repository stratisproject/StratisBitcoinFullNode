using HashLib;

namespace Stratis.SmartContracts.Core.Hashing
{
    /// <summary>
    /// Utilities to aid with hashing of data.
    /// </summary>
    public static class HashHelper
    {
        /// <summary>
        /// Returns a 32-byte Keccak256 hash of the given bytes.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static byte[] Keccak256(byte[] input)
        {
            return HashFactory.Crypto.SHA3.CreateKeccak256().ComputeBytes(input).GetBytes();
        }
    }
}