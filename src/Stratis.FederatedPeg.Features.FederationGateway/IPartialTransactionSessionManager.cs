using System;
using System.Threading.Tasks;
using NBitcoin;

//todo: this is pre-refactoring code
//todo: ensure no duplicate or fake withdrawal or deposit transactions are possible (current work underway)

namespace Stratis.FederatedPeg.Features.FederationGateway
{
    public interface IPartialTransactionSessionManager : IDisposable
    {
        void Initialize();

        uint256 CreateBuildAndBroadcastSession(CrossChainTransactionInfo crossChainTransactionInfo);

        uint256 CreateSessionOnCounterChain(uint256 transactionId, Money amount, string detinationAddress);

        Task<uint256> CreatePartialTransactionSession(uint256 transactionId, Money amount, string detinationAddress);

        void ReceivePartial(uint256 sessionId, Transaction partialTransaction, uint256 bossCard);

        PartialTransactionSession VerifySession(uint256 sessionId, Transaction partialTransactionTemplate);

        void MarkSessionAsSigned(PartialTransactionSession session);
    }
}
