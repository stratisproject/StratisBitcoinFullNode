using FluentAssertions;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.FederatedPeg.IntegrationTests.Utils;
using Xunit;

namespace Stratis.FederatedPeg.IntegrationTests
{
    public class NodeSetupTests : TestBase
    {
        [Fact]
        public void NodeSetup()
        {
            this.StartNodes(Chain.Main);

            this.MainAndSideChainNodeMap["mainUser"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedMain1"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedMain2"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedMain3"].Node.State.Should().Be(CoreNodeState.Running);

            this.StartNodes(Chain.Side);

            this.MainAndSideChainNodeMap["sideUser"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedSide1"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedSide2"].Node.State.Should().Be(CoreNodeState.Running);
            this.MainAndSideChainNodeMap["fedSide3"].Node.State.Should().Be(CoreNodeState.Running);
        }
    }
}
