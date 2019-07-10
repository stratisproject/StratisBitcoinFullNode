using Stratis.Bitcoin.Features.BlockStore.AddressIndexing;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.BlockStore
{
    public sealed class AddressIndexerIntegrationTests
    {
        [Fact]
        public void CanIndexAddreses()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinRegTest();

                var nodeConfig = new NodeConfigParameters
                {
                    { "-addressindex", "1" }
                };

                CoreNode stratisNode1 = builder.CreateStratisPowNode(network, "ia-1-stratisNode1", configParameters: nodeConfig).WithDummyWallet().Start();
                CoreNode stratisNode2 = builder.CreateStratisPowNode(network, "ia-1-stratisNode2", configParameters: nodeConfig).WithDummyWallet().Start();
                CoreNode stratisNode3 = builder.CreateStratisPowNode(network, "ia-1-stratisNode3", configParameters: nodeConfig).WithDummyWallet().Start();

                // Connect all the nodes.
                TestHelper.Connect(stratisNode1, stratisNode2);
                TestHelper.Connect(stratisNode1, stratisNode3);
                TestHelper.Connect(stratisNode2, stratisNode3);

                // Mine up to a height of 100.
                TestHelper.MineBlocks(stratisNode1, 100);

                TestBase.WaitLoop(() => stratisNode1.FullNode.NodeService<IAddressIndexer>().IndexerTip.Height == 100);
                TestBase.WaitLoop(() => stratisNode2.FullNode.NodeService<IAddressIndexer>().IndexerTip.Height == 100);
                TestBase.WaitLoop(() => stratisNode3.FullNode.NodeService<IAddressIndexer>().IndexerTip.Height == 100);
            }
        }
    }
}
