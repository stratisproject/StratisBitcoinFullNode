using Stratis.Bitcoin.Features.PoA.IntegrationTests.Tools;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.IntegrationTests
{
    public class NodeGeneralTests
    {
        [Fact]
        public void NodeCanStartAndStop()
        {
            var network = new PoANetwork();

            using (PoANodeBuilder builder = PoANodeBuilder.CreatePoANodeBuilder(this))
            {
                CoreNode node = builder.CreatePoANode(network).NotInIBD().Start();
                node.FullNode.Dispose();
            }
        }
    }
}
