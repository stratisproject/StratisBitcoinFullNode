using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.SmartContracts.CLR.Caching
{
    public interface IContractAssemblyCache
    {
        /// <summary>
        /// Stores a cached module definition for contract code.
        ///
        /// If trying to add a cache entry twice for the same hash, this will throw an exception.
        /// </summary>
        void Store(uint256 codeHash, CachedAssemblyPackage assembly);

        /// <summary>
        /// Returns a cached module definition for this contract code if one exists.
        ///
        /// Returns null if this contract code has not been cached yet.
        /// </summary>
        CachedAssemblyPackage Retrieve(uint256 codeHash);
    }

    public class ContractAssemblyCache : IContractAssemblyCache
    {
        private readonly Dictionary<uint256, CachedAssemblyPackage> cachedContracts;

        private readonly ILogger logger;

        public ContractAssemblyCache(ILoggerFactory loggerFactory)
        {
            this.cachedContracts = new Dictionary<uint256, CachedAssemblyPackage>();
            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <inheritdoc />
        public void Store(uint256 codeHash, CachedAssemblyPackage assembly)
        {
            this.cachedContracts[codeHash] = assembly;
        }

        /// <inheritdoc />
        public CachedAssemblyPackage Retrieve(uint256 codeHash)
        {
            CachedAssemblyPackage ret;
            this.cachedContracts.TryGetValue(codeHash, out ret);
            return ret;
        }
    }
}
