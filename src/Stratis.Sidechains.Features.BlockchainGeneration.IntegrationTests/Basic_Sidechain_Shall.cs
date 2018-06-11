using Xunit;
using FluentAssertions;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedSidechains.IntegrationTests.Common;
using Stratis.Sidechains.Features.BlockchainGeneration.Network;
namespace Stratis.Sidechains.Features.BlockchainGeneration.IntegrationTests
{
    [Collection("SidechainIdentifierTests")]
    public class Basic_Sidechain_Shall
    {

        //basic test that tests we can get to CoreNodeState.Running.
        [Fact]
        public void Start_Up()
        {
            using (var nodeBuilder = NodeBuilder.Create(this))
            {
                var coreNode = nodeBuilder.CreatePowPosMiningNode(SidechainNetwork.SidechainRegTest);
                coreNode.Start();
                coreNode.State.Should().Be(CoreNodeState.Running);
            }
        }
    }
}
