using System;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerTests
    {
        private readonly Network network;

        public ConsensusManagerTests()
        {
            this.network = new StratisRegTest();

            Type consensusType = typeof(NBitcoin.Consensus);
            consensusType.GetProperty("MaxReorgLength").SetValue(this.network.Consensus, (uint)20);

            this.network.Consensus.Options = new ConsensusOptionsTest();
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 10 ? 5 : 10;
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_MinerNode_Reorgs_AndResyncs_ToBestHeight()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.network);
                var minerB = builder.CreateStratisPosNode(this.network);
                var syncer = builder.CreateStratisPosNode(this.network);

                builder.StartAll();

                minerA.NotInIBD().WithWallet();
                minerB.NotInIBD().WithWallet();
                syncer.NotInIBD();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // Miner A continues to mine to height 20 whilst disconnected.
                TestHelper.MineBlocks(minerA, 10);

                // Miner B continues to mine to height 14 whilst disconnected.
                TestHelper.MineBlocks(minerB, 4);

                // Syncer now connects to both miners causing a re-org to occur for Miner B back to height 10
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Ensure that Syncer has synced with Miner A and Miner B.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 20);
            }
        }
    }
}