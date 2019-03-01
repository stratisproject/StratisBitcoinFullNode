using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.Miners
{
    public class ProofOfWorkMiningTests
    {
        private readonly BitcoinRegTest regTest;

        public ProofOfWorkMiningTests()
        {
            this.regTest = new BitcoinRegTest();
        }

        [Fact]
        public void MiningAndPropagatingPOW_MineBlockCheckPeerHasNewBlock()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode miner = builder.CreateStratisPowNode(this.regTest).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                CoreNode syncerA = builder.CreateStratisPowNode(this.regTest).Start();
                CoreNode syncerB = builder.CreateStratisPowNode(this.regTest).Start();
                CoreNode syncerC = builder.CreateStratisPowNode(this.regTest).Start();

                // Connect miner to all 3 syncers.
                TestHelper.Connect(miner, syncerA);
                TestHelper.Connect(miner, syncerB);
                TestHelper.Connect(miner, syncerC);

                // Ensure miner has 3 connections.
                TestHelper.WaitLoop(() => miner.FullNode.ConnectionManager.ConnectedPeers.Count() == 3);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(miner, syncerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(miner, syncerB));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(miner, syncerC));

                Assert.True(new[] { syncerA, syncerB, syncerC }.All(n => n.FullNode.ConsensusManager().Tip.Height == 10));
            }
        }

        [Fact]
        public async Task MiningAndPropagatingPOW_MineBlockNotPushedToConsensusCode_SupercededByBetterBlockOnReorg_InitialBlockRejectedAsync()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                CoreNode node1 = builder.CreateStratisPowNode(this.regTest).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10NoWallet).WithDummyWallet().Start();
                CoreNode node2 = builder.CreateStratisPowNode(this.regTest).WithDummyWallet().Start();

                TestHelper.ConnectAndSync(node1, node2);
                Assert.True(node1.FullNode.ConsensusManager().Tip.Height == 10);

                TestHelper.Disconnect(node1, node2);

                // Create block manually on node1 without pushing to consensus (BlockMined wont be called).
                Block manualBlock = TestHelper.GenerateBlockManually(node1, new List<Transaction>(), 0, false);

                // Mine another 2 blocks on node 2 chain up to height 7.
                TestHelper.MineBlocks(node2, 2);

                // Nodes 1 syncs with node 2 up to height 7.
                TestHelper.ConnectAndSync(node1, node2);
                Assert.True(node1.FullNode.ConsensusManager().Tip.Height == 12);

                // Call BlockMinedAsync manually with the block that was supposed to have been submitted at height 6.
                await node1.FullNode.ConsensusManager().BlockMinedAsync(manualBlock);

                // Verify that the manually added block is NOT in the consensus chain.
                var chainedHeaderToValidate = node1.FullNode.ConsensusManager().Tip.Previous;
                Assert.True(chainedHeaderToValidate.Height == 11);
                Assert.False(chainedHeaderToValidate.HashBlock == manualBlock.GetHash());
            }
        }
    }
}
