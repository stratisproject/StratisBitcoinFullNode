using System;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    public interface IMonitorChainSessionManager : IDisposable
    {
        void Initialize();

        void RegisterMonitorSession(MonitorChainSession monitorSession);

        void CreateSessionOnCounterChain(int apiPort, MonitorChainSession monitorChainSession);
    }
}
