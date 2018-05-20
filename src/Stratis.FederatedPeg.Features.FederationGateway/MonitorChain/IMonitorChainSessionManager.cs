using System;
using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    public interface IMonitorChainSessionManager : IDisposable
    {
        void Initialize();

        void CreateMonitorSession(CrossChainTransactionInfo crossChainTransactionInfo);
    }
}
