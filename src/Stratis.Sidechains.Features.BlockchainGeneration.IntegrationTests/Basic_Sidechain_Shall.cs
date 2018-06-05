using Xunit;
using FluentAssertions;
using Stratis.Sidechains.Features.BlockchainGeneration.Tests.Common.EnvironmentMockUp;

namespace Stratis.Sidechains.Features.BlockchainGeneration.IntegrationTests
{
    [Collection("SidechainIdentifierTests")]
    public class Basic_Sidechain_Shall
    {

        //basic test that tests we can get to CoreNodeState.Running.
        [Fact]
        public void Start_Up()
        {
            using (var nodeBuilder = NodeBuilder.Create())
            {
                var coreNode = nodeBuilder.CreatePosSidechainNode("enigma", false);
                coreNode.Start();
                coreNode.State.Should().Be(CoreNodeState.Running);
            }
        }
    }
}
