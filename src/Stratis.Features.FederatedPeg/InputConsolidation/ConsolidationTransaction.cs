using System;
using System.Collections.Generic;
using System.Text;
using DBreeze.Transactions;
using Stratis.Features.FederatedPeg.Interfaces;

namespace Stratis.Features.FederatedPeg.InputConsolidation
{
    public class ConsolidationTransaction
    {
        public Transaction PartialTransaction { get; set; }

        /// <summary>
        /// Note this should never be Suspended or Rejected. Only Partial, FullySigned or SeenInBlock
        /// </summary>
        public CrossChainTransferStatus Status { get; set; }
    }
}
