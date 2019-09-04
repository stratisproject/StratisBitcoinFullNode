using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Features.FederatedPeg.Events;
using Stratis.Features.FederatedPeg.InputConsolidation;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    /// <summary>
    /// Consolidates inputs into transactions to lighten the load of the wallet.
    /// </summary>
    public interface IInputConsolidator
    {
        /// <summary>
        /// The transactions being signed to consolidate inputs. Only in memory.
        /// </summary>
        List<ConsolidationTransaction> ConsolidationTransactions { get; }

        /// <summary>
        /// Trigger the building and signing of a consolidation transaction.
        /// </summary>
        void StartConsolidation(WalletNeedsConsolidation trigger);

        /// <summary>
        /// Attempt to merge the signatures of the incoming transaction and the current consolidation transaction.
        /// </summary>
        ConsolidationSignatureResult CombineSignatures(Transaction partialTransaction);

        /// <summary>
        /// Make any required changes to the consolidator's state as new blocks come in.
        /// </summary>
        void ProcessBlock(ChainedHeaderBlock chainedHeaderBlock);

        /// <summary>
        /// Make any required changes to the consolidator's state as new transactions come in.
        /// </summary>
        void ProcessTransaction(Transaction transaction);
    }
}
