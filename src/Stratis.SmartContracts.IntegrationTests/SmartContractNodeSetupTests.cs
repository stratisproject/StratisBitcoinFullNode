using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Networks;
using Stratis.SmartContracts.Tests.Common;
using Xunit;

namespace Stratis.SmartContracts.IntegrationTests
{
    public class SmartContractNodeSetupTests
    {
        [Fact(Skip = "Investigate PeerConnector shutdown timeout issue")]
        public void Mainnet_RequireStandard_False()
        {
            var network = new FakeSmartContractMain();
            Assert.False(network.IsTest());

            using (SmartContractNodeBuilder builder = SmartContractNodeBuilder.Create(this))
            {
                var node = builder.CreateSmartContractPoANode(network, 0);
                node.Start();
                TestHelper.WaitLoop(() => node.State == CoreNodeState.Running);
                Assert.False(node.FullNode.NodeService<MempoolSettings>().RequireStandard);
            }
        }

        private class FakeSmartContractMain : SmartContractsPoARegTest
        {
            public FakeSmartContractMain()
            {
                this.Name = "MainnetName"; // Doesn't contain "test" so IsTest() returns false.
            }
        }
    }
}
