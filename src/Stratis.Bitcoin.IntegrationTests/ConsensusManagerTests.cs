using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
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
        private readonly Network posNetwork;

        private readonly Network powNetwork;

        public ConsensusManagerTests()
        {
            this.posNetwork = new StratisRegTest();
            this.powNetwork = new BitcoinRegTest();

            Type consensusType = typeof(NBitcoin.Consensus);
            consensusType.GetProperty("MaxReorgLength").SetValue(this.posNetwork.Consensus, (uint)20);
        }

        private class ConsensusOptionsTest : PosConsensusOptions
        {
            public override int GetStakeMinConfirmations(int height, Network network)
            {
                return height < 15 ? 5 : 20;
            }
        }

        public class StratisOverrideRegTest : StratisRegTest
        {
            public StratisOverrideRegTest() : base()
            {
                this.Name = Guid.NewGuid().ToString();
            }
        }

        public class BitcoinOverrideRegTest : BitcoinRegTest
        {
            public BitcoinOverrideRegTest() : base()
            {
                this.Name = Guid.NewGuid().ToString();
            }
        }

        public class FailValidation : FullValidationConsensusRule
        {
            private readonly int failheight;
            private int failcount;

            public FailValidation(int failheight, int failcount = 1)
            {
                this.failheight = failheight;
                this.failcount = failcount;
            }

            public override Task RunAsync(RuleContext context)
            {
                if (this.failcount > 0)
                {
                    if (context.ValidationContext.ChainedHeaderToValidate.Height == this.failheight)
                    {
                        this.failcount -= 1;
                        throw new ConsensusErrorException(new ConsensusError("error", "error"));
                    }
                }

                return Task.CompletedTask;
            }
        }

        [Fact]
        public void ConsensusManager_Fork_Occurs_Node_Reorgs_AndResyncs_ToBestHeight()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var minerB = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var syncer = builder.CreateStratisPosNode(this.posNetwork).NotInIBD();

                builder.StartAll();

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
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var minerB = builder.CreateStratisPosNode(this.posNetwork).NotInIBD();
                var syncer = builder.CreateStratisPosNode(this.posNetwork).NotInIBD();

                builder.StartAll();

                // MinerA mines to height 15.
                TestHelper.MineBlocks(minerA, 15);

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
                minter.Stake(new WalletSecret() { WalletName = "mywallet", WalletPassword = "password" });

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

        [Retry]
        public void ConsensusManager_Fork_Occurs_Node_Gets_Disconnected_Due_To_MaxReorgViolation()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var minerB = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var syncer = builder.CreateStratisPosNode(this.posNetwork).NotInIBD();

                builder.StartAll();

                // MinerA mines to height 20.
                TestHelper.MineBlocks(minerA, 20);

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
                TestHelper.MineBlocks(minerA, 25);

                // MinerB continues to mine to height 65.
                TestHelper.MineBlocks(minerB, 45);

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

        [Fact]
        public void ConsensusManager_Reorgs_Then_Old_Chain_Becomes_Longer_Then_Reorg_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet();
                var syncer = builder.CreateStratisPowNode(this.powNetwork).NotInIBD();

                builder.StartAll();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Enable syncer to send blocks to miner B
                TestHelper.EnableBlockPropagation(syncer, minerB);

                // Disable syncer from sending blocks to miner A
                TestHelper.DisableBlockPropagation(syncer, minerA);

                // Miner B continues to mine to height 30 on a new and longer chain whilst disconnected.
                TestHelper.MineBlocks(minerB, 20);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                // Enable syncer to send blocks to miner B
                TestHelper.EnableBlockPropagation(syncer, minerA);

                // Miner A mines to height 40.
                TestHelper.MineBlocks(minerA, 20);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));

                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 40);
                Assert.True(minerA.FullNode.ConsensusManager().Tip.Height == 40);
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 40);
            }
        }

        [Retry]
        public void ConsensusManager_Reorgs_Then_Try_To_Connect_Longer_Chain__With_Connected_Blocks_And_Fail_Then_Revert_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new StratisOverrideRegTest();

                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var minerB = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var syncer = builder.CreateStratisPosNode(syncerNetwork).NotInIBD();

                builder.StartAll();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Inject a rule that will fail at block 15 of the new chain.
                ConsensusRuleEngine engine = syncer.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidation(15));
                engine.Register();

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 30);

                // Miner B should become disconnected.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back.
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Check syncer is still synced with Miner A.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void ConsensusManager_Reorgs_Then_Try_To_Connect_Longer_Chain__With__No_Connected_Blocks_And_Fail_Then_Revert_Back()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new BitcoinOverrideRegTest();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).NotInIBD().WithWallet();
                var syncer = builder.CreateStratisPowNode(syncerNetwork).NotInIBD();

                builder.StartAll();

                // MinerA mines to height 10.
                TestHelper.MineBlocks(minerA, 10);

                // Sync the network to height 10.
                TestHelper.ConnectAndSync(syncer, minerA, minerB);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(syncer, minerB);

                // Miner A and syncer continues to mine to height 20.
                TestHelper.MineBlocks(minerA, 10);
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));

                // Inject a rule that will fail at block 11 of the new chain
                ConsensusRuleEngine engine = syncer.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidation(11));
                engine.Register();

                // Miner B continues to mine to height 30 on a new and longer chain.
                TestHelper.MineBlocks(minerB, 20);

                // check miner B at height 30.
                Assert.True(minerB.FullNode.ConsensusManager().Tip.Height == 30);

                // Miner B should become disconnected.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerB));

                // Make sure syncer rolled back
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 20);

                // Check syncer is still synced with Miner A
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerA));
            }
        }

        [Fact]
        public void ConsensusManager_Connect_New_Block_Failed()
        {
            using (NodeBuilder builder = NodeBuilder.Create(this))
            {
                var syncerNetwork = new StratisOverrideRegTest();

                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet();
                var syncer = builder.CreateStratisPosNode(syncerNetwork).NotInIBD();

                builder.StartAll();

                // Miner A mines to height 11.
                TestHelper.MineBlocks(minerA, 11);

                // Inject a rule that will fail at block 11 of the new chain
                ConsensusRuleEngine engine = syncer.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidation(11));
                engine.Register();

                // Connect syncer to Miner A, reorg should fail.
                TestHelper.Connect(syncer, minerA);

                // Syncer should disconnect from miner A after the failed block.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(syncer, minerA));

                // Make sure syncer rolled back
                Assert.True(syncer.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }
    }
}