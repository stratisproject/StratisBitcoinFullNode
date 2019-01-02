using System;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public class ProofOfStakeMiningTests
    {
        private class StratisRegTestLastPowBlock : StratisRegTest
        {
            public StratisRegTestLastPowBlock()
            {
                this.Name = Guid.NewGuid().ToString();
            }
        }

        [Fact]
        public void MiningAndPropagatingPOS_MineBlockCheckPeerHasNewBlock()
        {
            using (NodeBuilder nodeBuilder = NodeBuilder.Create(this))
            {
                var network = new StratisRegTest();

                CoreNode node = nodeBuilder.CreateStratisPosNode(network).WithDummyWallet().Start();
                CoreNode syncer = nodeBuilder.CreateStratisPosNode(network).Start();

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
                var network = new StratisRegTestLastPowBlock();

                CoreNode node = nodeBuilder.CreateStratisPosNode(network).WithDummyWallet().Start();

                // Mine two blocks (OK).
                TestHelper.MineBlocks(node, 2);

                // Mine another block after LastPOWBlock height (Error).
                node.FullNode.Network.Consensus.LastPOWBlock = 2;
                var error = Assert.Throws<ConsensusRuleException>(() => TestHelper.MineBlocks(node, 1));
                Assert.True(error.ConsensusError.Message == ConsensusErrors.ProofOfWorkTooHigh.Message);
            }
        }
    }
}