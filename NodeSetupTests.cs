using System.Linq;
using FluentAssertions;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.IntegrationTests.Common;
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

        [Fact(Skip ="Sidechain nodes starting but can't execute endpoints when enabling wallets - make sure sidechains in TestBase are running as normal.")]
        public void EnableNodeWallets()
        {
            this.StartAndConnectNodes();

            string[] ignoreNodes = { "mainUser", "sideUser" };

            this.EnableWallets(this.MainAndSideChainNodeMap.
                Where(k => !ignoreNodes.Contains(k.Key)).
                Select(v => v.Value.Node).ToList());
        }

        [Fact]
        public void FundMainChainNode()
        {
            this.StartNodes(Chain.Main);
            this.ConnectMainChainNodes();

            NodeChain mainUser = this.MainAndSideChainNodeMap["mainUser"];
            NodeChain fedMain1 = this.MainAndSideChainNodeMap["fedMain1"];
            NodeChain fedMain2 = this.MainAndSideChainNodeMap["fedMain2"];
            NodeChain fedMain3 = this.MainAndSideChainNodeMap["fedMain3"];

            TestHelper.MineBlocks(mainUser.Node, (int)this.mainchainNetwork.Consensus.CoinbaseMaturity + 1);
            TestHelper.WaitForNodeToSync(mainUser.Node, fedMain1.Node, fedMain2.Node, fedMain3.Node);

            Assert.Equal(this.mainchainNetwork.Consensus.ProofOfWorkReward, GetBalance(mainUser.Node));
        }

    }
}
