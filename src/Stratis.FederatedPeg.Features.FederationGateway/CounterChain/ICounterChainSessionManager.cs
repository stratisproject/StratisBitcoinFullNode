using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.CounterChain
{
    public interface ICounterChainSessionManager
    {
        void CreateSessionOnCounterChain(uint256 transactionId, Money amount, string detinationAddress);

        Task<uint256> ProcessCounterChainSession(uint256 transactionId, Money amount, string destinationAddress);

        void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard);

        /// <summary>
        /// VerifySession ensures it is safe to sign any inbound partial transaction by performing a
        /// number of checks against the session data. 
        /// </summary>
        /// <param name="sessionId">An id that identifies the session.</param>
        /// <param name="partialTransactionTemplate">The partial transaction we are asked to sign.</param>
        /// <returns></returns>
        CounterChainSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate);

        void MarkSessionAsSigned(CounterChainSession session);
    }
}
