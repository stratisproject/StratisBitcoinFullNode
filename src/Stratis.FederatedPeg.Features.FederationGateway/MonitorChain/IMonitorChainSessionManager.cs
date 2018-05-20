using NBitcoin;

namespace Stratis.FederatedPeg.Features.FederationGateway.MonitorChain
{
    public interface IMonitorChainSessionManager
    {
        void Initialize();

        uint256 CreateBuildAndBroadcastSession(CrossChainTransactionInfo crossChainTransactionInfo);
    }
}
