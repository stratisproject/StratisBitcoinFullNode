using NBitcoin;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.InputConsolidation
{
    public class ConsolidationTransaction
    {
        /// <summary>
        /// The physical transaction being signed and later sent.
        /// </summary>
        public Transaction PartialTransaction { get; set; }

        /// <summary>
        /// State this consolidation transaction is in.
        /// </summary>
        public ConsolidationTransactionStatus Status { get; set; }
    }
}
