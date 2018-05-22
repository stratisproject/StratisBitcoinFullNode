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

        CounterChainSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate);

        void MarkSessionAsSigned(CounterChainSession session);
    }
}
