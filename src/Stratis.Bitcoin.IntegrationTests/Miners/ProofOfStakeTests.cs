using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public class ProofOfStakeTests
    {
        [Fact]
        public void MiningAndPropagatingPOS_MineBlockCheckPeerHasNewBlock()
        {
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                CoreNode node = nodeBuilder.CreateStratisPosNode(new StratisRegTest()).NotInIBD().WithWallet();
                CoreNode syncer = nodeBuilder.CreateStratisPosNode(new StratisRegTest()).NotInIBD();
                nodeBuilder.StartAll();

                TestHelper.MineBlocks(node, 1);
                Assert.NotEqual(node.FullNode.ConsensusManager().Tip, syncer.FullNode.ConsensusManager().Tip);

                TestHelper.ConnectAndSync(node, syncer);
                Assert.Equal(node.FullNode.ConsensusManager().Tip, syncer.FullNode.ConsensusManager().Tip);
            }
        }

        [Fact]
        public void MiningAndPropagatingPOS_MineBlockStakeAtInsufficientHeightError()
        {
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                CoreNode node = nodeBuilder.CreateStratisPosNode(new StratisRegTest()).NotInIBD().WithWallet();
                nodeBuilder.StartAll();

                // Mine two blocks (OK).
                TestHelper.MineBlocks(node, 2);

                // Mine another block after LastPOWBlock height (Error).
                nodeBuilder.Nodes[0].FullNode.Network.Consensus.LastPOWBlock = 2;
                var error = Assert.Throws<ConsensusException>(() => TestHelper.MineBlocks(node, 1));
                Assert.True(error.Message == ConsensusErrors.ProofOfWorkTooHigh.Message);
            }
        }
    }
}