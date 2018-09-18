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

        private const string walletAccount = "account 0";
        private const string walletName = "mywallet";
        private const string walletPassword = "123456";

        public ConsensusManagerTests()
        {
            this.network = new StratisRegTest();
            this.network.Consensus.Options = new ConsensusOptionsTest();

            Type consensusType = typeof(NBitcoin.Consensus);
            consensusType.GetProperty("MaxReorgLength").SetValue(this.network.Consensus, (uint)20);
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 10 ? 5 : 10;
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_MaxReorgViolation()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.network, "minerA");
                var minerB = builder.CreateStratisPosNode(this.network, "minerB");
                var syncer = builder.CreateStratisPosNode(this.network, "syncer");

                builder.StartAll();

                minerA.NotInIBD().WithWallet();
                minerB.NotInIBD().WithWallet();
                syncer.NotInIBD();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 10);

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
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 10);

                // Miner B continues to mine to height 14 whilst disconnected.
                TestHelper.MineBlocks(minerB, walletName, walletPassword, walletAccount, 4);

                // Syncer now connects to both miners.
                TestHelper.Connect(syncer, minerA);
                TestHelper.Connect(syncer, minerB);

                // Ensure that Syncer has synced with Miner A which is the node with the best tip.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Miner A needs to mine 20 more blocks so that Miner B can be disconnected from syncer.
                TestHelper.MineBlocks(minerA, walletName, walletPassword, walletAccount, 20);

                // Ensure that Syncer is not connected to miner B any longer.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Reconnect Miner B to Miner A and ensure the sync
                TestHelper.Connect(minerB, minerA);
                TestHelper.IsNodeConnectedTo(minerB, minerA);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerA, minerB));
            }
        }
    }
}