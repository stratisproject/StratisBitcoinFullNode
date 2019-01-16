using Stratis.SmartContracts.Core.Hashing;

namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// This gets injected into the contract to provide implementations for the hashing of data. 
    /// </summary>
    public class InternalHashHelper : IInternalHashHelper
    {
        /// <summary>
        /// Returns a 32-byte Keccak256 hash of the given bytes.
        /// </summary>
        /// <param name="toHash"></param>
        /// <returns></returns>
        public byte[] Keccak256(byte[] toHash)
        {
            return HashHelper.Keccak256(toHash);
        }
    }
}