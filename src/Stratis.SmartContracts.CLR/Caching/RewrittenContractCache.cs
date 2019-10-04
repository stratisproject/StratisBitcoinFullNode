using System.Collections.Generic;
using Microsoft.Extensions.Logging;
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
        void Store(uint256 initialCodeHash, byte[] rewrittenContractCode);

        /// <summary>
        /// Returns a cached module definition for this contract code if one exists.
        ///
        /// Returns null if this contract code has not been cached yet.
        /// </summary>
        byte[] Retrieve(uint256 codeHash);
    }

    public class RewrittenContractCache : IRewrittenContractCache
    {
        private readonly Dictionary<uint256, byte[]> cachedContracts;

        private readonly ILogger logger;

        public RewrittenContractCache(ILoggerFactory loggerFactory)
        {
            this.cachedContracts = new Dictionary<uint256, byte[]>();

            this.logger = loggerFactory.CreateLogger(this.GetType());
        }

        /// <inheritdoc />
        public void Store(uint256 initialCodeHash, byte[] rewrittenContractCode)
        {
            if (this.cachedContracts.ContainsKey(initialCodeHash))
            {
                // This could happen in a race condition where multiple threads are using the same contract for the first time at the same time.
                this.logger.LogWarning("Tried to add the same contract code to the cache twice.");
                return;
            }

            this.cachedContracts[initialCodeHash] = rewrittenContractCode;
        }

        /// <inheritdoc />
        public byte[] Retrieve(uint256 codeHash)
        {
            byte[] ret = null;
            this.cachedContracts.TryGetValue(codeHash, out ret);
            return ret;
        }
    }
}
