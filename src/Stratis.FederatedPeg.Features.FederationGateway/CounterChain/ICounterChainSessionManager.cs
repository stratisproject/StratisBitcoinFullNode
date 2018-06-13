using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.CounterChain
{
    /// <summary>
    /// The CounterChainSessionManager receives session data from the MonitorChainSessionManager and takes a number of precautionary steps
    /// before signing the transaction. It also builds and broadcasts the transaction once a quorum of is received from the payload
    /// </summary>
    public interface ICounterChainSessionManager
    {
        /// <summary>
        /// Called from our sister MonitorChainSessionManager.  The monitor is reading our trusted chain and
        /// this registers the session data that tells us the trusted data we must use to verify we are signing
        /// a true transaction that has not been corrupted by a rouge or alien actor.
        /// </summary>
        /// <param name="sessionId">The transaction hash of the source transaction from the monior chain is used as the sessionId.</param>
        /// <param name="amount">The amount of the transaction.</param>
        /// <param name="destinationAddress">The final destination address on this counterchain.</param>
        void CreateSessionOnCounterChain(uint256 transactionId, Money amount, string detinationAddress);

        /// <summary>
        /// Do the work to process the transaction. In this method we start the process of requesting peer gateways to sign our transaction
        /// by building the partial transaction template and broadcasting it to peers.
        /// </summary>
        /// <param name="transactionId">The transactionId that forms the session Id.</param>
        /// <param name="amount">The amount of the transaction.</param>
        /// <param name="destinationAddress">The final destination address.</param>
        /// <returns></returns>
        Task<uint256> ProcessCounterChainSession(uint256 transactionId, Money amount, string destinationAddress);

        /// <summary>
        /// Receives a partial transaction inbound from the payload behavior.
        /// </summary>
        /// <param name="sessionId">The session we are receiving.</param>
        /// <param name="partialTransaction">Inbound partial transaction.</param>
        /// <param name="bossCard">The insertion place in the partial transaction table we store in the session.</param>
        void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard);

        /// <summary>
        /// VerifySession ensures it is safe to sign any inbound partial transaction by performing a
        /// number of checks against the session data. 
        /// </summary>
        /// <param name="sessionId">An id that identifies the session.</param>
        /// <param name="partialTransactionTemplate">The partial transaction we are asked to sign.</param>
        /// <returns></returns>
        CounterChainSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate);

        /// <summary>
        /// Record that we have signed the session.
        /// </summary>
        /// <param name="session">The session.</param>
        void MarkSessionAsSigned(CounterChainSession session);

        /// <summary>
        /// Imports the federation member's mnemonic key.
        /// </summary>
        /// <param name="password">The user's password.</param>
        /// <param name="mnemonic">The user's mnemonic.</param>
        void ImportMemberKey(string password, string mnemonic);
    }
}
