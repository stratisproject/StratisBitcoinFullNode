using System;
using System.Collections.Generic;
using Mono.Cecil;
using NBitcoin;

namespace Stratis.SmartContracts.CLR.Caching
{
    public interface IRewrittenContractCache
    {
        /// <summary>
        /// Stores a cached module definition for contract code.
        ///
        /// If trying to add a cache entry twice for the same hash, this will throw an exception.
        /// </summary>
        void Store(uint256 initialCodeHash, ModuleDefinition module);

        /// <summary>
        /// Returns a cached module definition for this contract code if one exists.
        ///
        /// Returns null if this contract code has not been cached yet.
        /// </summary>
        ModuleDefinition Retrieve(uint256 codeHash);
    }
    public class RewrittenContractCache : IRewrittenContractCache
    {
        private readonly Dictionary<uint256, ModuleDefinition> cachedContracts;

        public RewrittenContractCache()
        {
            this.cachedContracts = new Dictionary<uint256, ModuleDefinition>();
        }

        /// <inheritdoc />
        public void Store(uint256 initialCodeHash, ModuleDefinition module)
        {
            if (this.cachedContracts.ContainsKey(initialCodeHash))
                throw new Exception("Cache already contains an entry for this code hash.");

            this.cachedContracts.Add(initialCodeHash, module);
        }

        /// <inheritdoc />
        public ModuleDefinition Retrieve(uint256 codeHash)
        {
            if (this.cachedContracts.ContainsKey(codeHash))
                return this.cachedContracts[codeHash];

            return null;
        }
    }
}
