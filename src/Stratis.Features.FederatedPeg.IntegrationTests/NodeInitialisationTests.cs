using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Features.FederatedPeg.IntegrationTests.Utils;
using Stratis.Sidechains.Networks;
using Xunit;

namespace Stratis.Features.FederatedPeg.IntegrationTests
{
    /// <summary>
    /// These tests help detect any issues with the DI or initialisation of the nodes.
    /// </summary>
    public class NodeInitialisationTests
    {
        [Fact]
        public void SidechainUserStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                CirrusRegTest network = (CirrusRegTest)CirrusNetwork.NetworksSelector.Regtest();

                CoreNode user = nodeBuilder.CreateSidechainNode(network);

                user.Start();

                Assert.Equal(CoreNodeState.Running, user.State);
            }
        }


        [Fact]
        public void SidechainMinerStarts()
        {
            using (SidechainNodeBuilder nodeBuilder = SidechainNodeBuilder.CreateSidechainNodeBuilder(this))
            {
                CirrusRegTest network = (CirrusRegTest)CirrusNetwork.NetworksSelector.Regtest();
                Network counterChainNetwork = Networks.Stratis.Regtest();
                Key federationKey = new Key();

                CoreNode miner = nodeBuilder.CreateSidechainMinerNode(network, counterChainNetwork, federationKey);

                miner.Start();

                Assert.Equal(CoreNodeState.Running, miner.State);
            }
        }

        [Fact]
        public void SidechainFederationStarts()
        {

        }

        [Fact]
        public void MainChainMinerStarts()
        {

        }

        [Fact]
        public void MainChainFederationStarts()
        {

        }

    }
}
