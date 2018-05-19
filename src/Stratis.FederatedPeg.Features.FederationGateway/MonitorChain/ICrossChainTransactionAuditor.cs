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
        // Sets up the auditor.
        void Initialize();

        // Adds the initiating transaction info for a cross chain transaction.
        void AddCrossChainTransactionInfo(CrossChainTransactionInfo crossChainTransactionInfo);

        // Loads the auditor data if required.
        void Load();

        // Commits the audit to persistent storage.
        void Commit();

        // Adds the identifier for the transaction on the counter chain.
        void AddCounterChainTransactionId(uint256 monitorTransactionHash, uint256 counterChainTransactionHash);
    }
}
