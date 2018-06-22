using System.Collections.Concurrent;
using System.Linq;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// A store used to hold information about cross chain transactions. This store is also persisted to disk. 
    /// </summary>
    internal sealed class JsonCrossChainTransactionStore
    {
        /// <summary>
        /// Returns the count of items in the store.
        /// </summary>
        public int Count => this.CrossChainTransactionInfos.Count;

        /// Construct the store.
        /// </summary>
        public JsonCrossChainTransactionStore()
        {
            this.CrossChainTransactionInfos = new ConcurrentBag<CrossChainTransactionInfo>();
        }

        // In memory structure used for the storage of cross chain transaction infos.
        public ConcurrentBag<CrossChainTransactionInfo> CrossChainTransactionInfos { get; }
        
        /// <summary>
        /// Add info to the store.
        /// </summary>
        /// <param name="crossChainTransactionInfo"></param>
        public void Add(CrossChainTransactionInfo crossChainTransactionInfo)
        {
            this.CrossChainTransactionInfos.Add(crossChainTransactionInfo);
        }

        // todo: convert to a dictionary
        // todo: include proper exception handing

        public void AddCrossChainTransactionId(uint256 sessionId, uint256 crossChainTransactionId)
        {
            var crossChainTransactionInfo = this.CrossChainTransactionInfos.FirstOrDefault(c => c.TransactionHash == sessionId);
            if (crossChainTransactionInfo != null)
            {
                crossChainTransactionInfo.CrossChainTransactionId = crossChainTransactionId;
            }
        }
    }
}