using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.CounterChain
{
    public interface ICounterChainSessionManager
    {
        uint256 CreateSessionOnCounterChain(uint256 transactionId, Money amount, string detinationAddress);

        Task<uint256> CreatePartialTransactionSession(uint256 transactionId, Money amount, string destinationAddress);

        void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard);

        PartialTransactionSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate);

        void MarkSessionAsSigned(PartialTransactionSession session);
    }
}
