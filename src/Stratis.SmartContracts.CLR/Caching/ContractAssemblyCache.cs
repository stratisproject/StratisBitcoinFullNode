using System.Collections.Generic;
using NBitcoin;

namespace Stratis.SmartContracts.CLR.Caching
{
    public interface IContractAssemblyCache
    {
        /// <summary>
        /// Stores a cached assembly and module definition for a particular contract's code.
        ///
        /// If trying to add a cache entry twice for the same hash, this will throw an exception.
        /// </summary>
        void Store(uint256 codeHash, CachedAssemblyPackage assembly);

        /// <summary>
        /// Returns a cached assembly and module definition for this contract code.
        ///
        /// Returns null if this contract code has not been cached yet.
        /// </summary>
        CachedAssemblyPackage Retrieve(uint256 codeHash);
    }

    public class ContractAssemblyCache : IContractAssemblyCache
    {
        private readonly Dictionary<uint256, CachedAssemblyPackage> cachedContracts;

        public ContractAssemblyCache()
        {
            this.cachedContracts = new Dictionary<uint256, CachedAssemblyPackage>();
        }

        /// <inheritdoc />
        public void Store(uint256 codeHash, CachedAssemblyPackage assembly)
        {
            this.cachedContracts[codeHash] = assembly;
        }

        /// <inheritdoc />
        public CachedAssemblyPackage Retrieve(uint256 codeHash)
        {
            this.cachedContracts.TryGetValue(codeHash, out CachedAssemblyPackage ret);
            return ret;
        }
    }
}
