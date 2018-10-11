using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public class ProofOfStakeMiningTests
    {
        [Fact]
        public void MiningAndPropagatingPOS_MineBlockCheckPeerHasNewBlock()
        {
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode node = nodeBuilder.CreateStratisPosNode(network).NotInIBD().WithWallet();
                CoreNode syncer = nodeBuilder.CreateStratisPosNode(network).NotInIBD();
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
                var network = new StratisRegTest();

                CoreNode node = nodeBuilder.CreateStratisPosNode(network).NotInIBD().WithWallet();
                nodeBuilder.StartAll();

                // Mine two blocks (OK).
                TestHelper.MineBlocks(node, 2);

                // Mine another block after LastPOWBlock height (Error).
                var lastPowBlock = node.FullNode.Network.Consensus.LastPOWBlock;
                node.FullNode.Network.Consensus.LastPOWBlock = 2;
                var error = Assert.Throws<ConsensusException>(() => TestHelper.MineBlocks(node, 1));
                Assert.True(error.Message == ConsensusErrors.ProofOfWorkTooHigh.Message);
                node.FullNode.Network.Consensus.LastPOWBlock = lastPowBlock;
            }
        }
    }
}