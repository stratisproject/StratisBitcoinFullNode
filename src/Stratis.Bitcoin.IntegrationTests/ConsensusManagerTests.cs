using System;
using NBitcoin;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
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
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 15 ? 5 : 20;
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Reorgs_AndResyncs_ToBestHeight()
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
                TestHelper.Connect(minerA, syncer);
                TestHelper.Connect(minerB, minerA);

                // Ensure that Syncer has synced with Miner A and Miner B.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerA, syncer));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerB, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 20);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 20);
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_InvalidStakeDepth()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.network);
                var minerB = builder.CreateStratisPosNode(this.network);
                var syncer = builder.CreateStratisPosNode(this.network);

                builder.StartAll();

                minerA.NotInIBD().WithWallet();
                minerB.NotInIBD();
                syncer.NotInIBD();

                // MinerA mines to height 15.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 15);

                // Sync the network to height 15.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // Miner A stakes a coin that increases the network height to 16.
                var minter = minerA.FullNode.NodeService<IPosMinting>();
                minter.Stake(new WalletSecret() { WalletName = walletName, WalletPassword = walletPassword });

                TestHelper.WaitLoop(() =>
                {
                    return minerA.FullNode.ConsensusManager().Tip.Height == 16;
                });

                minter.StopStake();

                // Update the network consensus options so that the GetStakeMinConfirmations returns a higher value
                // to ensure that the InvalidStakeDepth exception can be thrown.
                minerA.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();
                minerB.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();
                syncer.FullNode.Network.Consensus.Options = new ConsensusOptionsTest();

                // Syncer now connects to both miners causing a InvalidStakeDepth exception to be thrown
                // on Miner A.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Ensure that Syncer is synced with MinerB.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Ensure that Syncer is not connected to MinerA.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerA));

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 15);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 15);
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_MaxReorgViolation()
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

                // MinerA mines to height 20.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 20);

                // Sync the network to height 20.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Disconnect Miner A and B.
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.Disconnect(syncer, minerB);

                // Ensure syncer does not have any connections.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(syncer));

                // MinerA continues to mine to height 45.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 25);

                // MinerB continues to mine to height 65.
                TestHelper.MineBlocks(minerB, walletName, walletPassword, walletAccount, 45);

                // Syncer now connects to both miners causing a MaxReorgViolation exception to be thrown
                // on Miner B.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Ensure that Syncer is synced with MinerA.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Ensure that Syncer is not connected to MinerB.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 45);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 45);
            }
        }
    }
}