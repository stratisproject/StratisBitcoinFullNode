using System.Collections.Concurrent;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// A store used to hold information about cross chain transactions. This store is also persisted to disk. 
    /// </summary>
    internal sealed class CrossChainTransactionStore
    {
        /// <summary>
        /// Returns the count of items in the store.
        /// </summary>
        public int Count => this.CrossChainTransactionInfos.Count;

        /// <summary>
        /// Construct the store.
        /// </summary>
        public CrossChainTransactionStore()
        {
            this.CrossChainTransactionInfos = new ConcurrentBag<CrossChainTransactionInfo>();
        }

        // In memory structure used for the storage of cross chain transaction infos.
        private ConcurrentBag<CrossChainTransactionInfo> CrossChainTransactionInfos { get; }
        
        /// <summary>
        /// Add info to the store.
        /// </summary>
        /// <param name="crossChainTransactionInfo"></param>
        public void Add(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            this.CrossChainTransactionInfos.Add(crossChainTransactionInfo);
        }
    }
}