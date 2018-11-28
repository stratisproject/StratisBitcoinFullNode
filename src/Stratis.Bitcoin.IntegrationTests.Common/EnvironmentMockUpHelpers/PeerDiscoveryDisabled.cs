using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.P2P;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    /// <summary>
    /// To be used with all the test runners to ensure that peer discovery does not run.
    /// </summary>
    public sealed class PeerDiscoveryDisabled : IPeerDiscovery
    {
        public void DiscoverPeers(IConnectionManager connectionManager)
        {
        }

        public void Dispose()
        {
        }
    }
}
