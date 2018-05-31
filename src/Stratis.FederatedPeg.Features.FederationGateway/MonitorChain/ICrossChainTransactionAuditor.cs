using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    /// <summary>
    /// The Monitor can use an Auditor to record all deposits and withdrawals that it
    /// receives from new blocks.  This involves recording information for two transactions
    /// (monitored chain and counter chain).
    /// </summary>
    public interface ICrossChainTransactionAuditor : IDisposable
    {
        /// <summary>
        /// Sets up the auditor.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Adds the initiating transaction info for a cross chain transaction.
        /// </summary>
        void AddCrossChainTransactionInfo(CrossChainTransactionInfo crossChainTransactionInfo);

        /// <summary>
        /// Loads the auditor data if required.
        /// </summary>
        void Load();

        /// <summary>
        /// Commits the audit to persistent storage.
        /// </summary>
        void Commit();

        /// <summary>
        /// Adds the identifier for the transaction on the counter chain.
        /// </summary>
        /// <param name="monitorTransactionHash">The source transaction hash used as the sessionId.</param>
        /// <param name="counterChainTransactionHash">The hash of the counter chain transaction.</param>
        void AddCounterChainTransactionId(uint256 monitorTransactionHash, uint256 counterChainTransactionHash);
    }
}
