using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.TestNetworks;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerFailedReorgTests
    {
        private readonly Network posNetwork;
        private readonly Network powNetwork;
        private readonly bool runDashboard;

        public ConsensusManagerFailedReorgTests(bool runDashboard = false)
        {
            this.posNetwork = new StratisRegTest();
            this.powNetwork = new BitcoinRegTest();

            this.runDashboard = runDashboard;
        }

        [Fact]
        public void ReorgChain_FailsFullValidation_Reconnect_OldChain_Nodes_Connected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var bitcoinNoValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(bitcoinNoValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

                // On mining the following will happen:
                // Reorg from blocks 9 to 5.
                // Connect blocks 5 to 10
                // Block 8 fails.
                // Reorg from 7 to 5
                // Reconnect blocks 6 to 9
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }

        public class FailValidationAtAttempt : FullValidationConsensusRule
        {
            private readonly int failheight;
            private int failAtAttempt;
            private int runCount;

            public FailValidationAtAttempt(int failheight, int failAtAttempt)
            {
                this.failheight = failheight;
                this.failAtAttempt = failAtAttempt;
            }

            public override Task RunAsync(RuleContext context)
            {
                if (context.ValidationContext.ChainedHeaderToValidate.Height == this.failheight)
                {
                    this.runCount += 1;

                    if (this.runCount == this.failAtAttempt)
                    {
                        throw new ConsensusErrorException(new ConsensusError("error", "error"));
                    }
                }

                return Task.CompletedTask;
            }
        }

        [Fact]
        public void TwoConsecutive_ReorgChain_FailsFullValidation_Reconnect_OldChain_Nodes_Connected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var bitcoinNoValidationRulesNetwork = new BitcoinRegTestNoValidationRules();
                var syncerNetwork = new ConsensusManagerTests.BitcoinOverrideRegTest();

                var minerA = builder.CreateStratisPowNode(syncerNetwork, "minerA").WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(bitcoinNoValidationRulesNetwork, "minerB").WithDummyWallet().Start();
                var minerC = builder.CreateStratisPowNode(bitcoinNoValidationRulesNetwork, "minerC").WithDummyWallet().Start();

                if(this.runDashboard)
                    builder.RunDashboard();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 15);

                // MinerB and MinerC syncs with MinerA
                TestHelper.ConnectAndSync(minerA, minerB, minerC);
                TestHelper.DisconnectAll(minerA, minerB, minerC);

                // Miner A continues to mine to height 20
                TestHelper.MineBlocks(minerA, 5);
                Assert.True(minerA.FullNode.Chain.Height == 20);

                TestHelper.MineBlocks(minerB, 6);
                Assert.True(minerB.FullNode.Chain.Height == 21);

                TestHelper.MineBlocks(minerC, 7);
                Assert.True(minerC.FullNode.Chain.Height == 22);


                // Inject a rule that will fail at block 19 of the new chain.
                var engine = minerA.FullNode.NodeService<IConsensusRuleEngine>() as ConsensusRuleEngine;
                syncerNetwork.Consensus.FullValidationRules.Insert(1, new FailValidationAtAttempt(19, 2));
                engine.Register();

                TestHelper.Connect(minerA, minerB);
                TestHelper.Connect(minerA, minerC);

                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerA, minerB));
                TestHelper.WaitLoop(() =>
                {
                    return
                        minerA.FullNode.ConsensusManager().Tip.Height == 22
                        && minerB.FullNode.ConsensusManager().Tip.Height == 22
                        && minerC.FullNode.ConsensusManager().Tip.Height == 22
                    ;
                });
            }
        }


        [Fact]
        public void ReorgChain_FailsFullValidation_Reconnect_OldChain_Nodes_Disconnected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disconnect miner B from minerA
                TestHelper.Disconnect(minerB, minerA);

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

                // Reconnect minerA to minerB causing the following to happen:
                // Reorg from blocks 9 to 5.
                // Connect blocks 5 to 10
                // Block 8 fails.
                // Reorg from 7 to 5
                // Reconnect blocks 6 to 9
                TestHelper.Connect(minerA, minerB);

                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }

        [Fact]
        public void ReorgChain_FailsFullValidation_Reconnect_OldChain_FromSecondMiner_Disconnected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var syncer = builder.CreateStratisPowNode(this.powNetwork).Start();
                var minerB = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB and Syncer syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);
                TestHelper.ConnectAndSync(syncer, minerA);

                // Disconnect minerB from miner A
                TestHelper.Disconnect(minerB, minerA);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(minerB));

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 5);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 9);

                // Disconnect syncer from minerA
                TestHelper.Disconnect(syncer, minerA);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(minerA));

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

                // Reconnect syncer to minerB causing the following to happen:
                // Reorg from blocks 9 to 5.
                // Connect blocks 5 to 10
                // Block 8 fails.
                // Reorg from 7 to 5
                // Reconnect blocks 6 to 9
                TestHelper.Connect(syncer, minerB);

                TestHelper.AreNodesSynced(minerA, syncer);

                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 9);
            }
        }

        [Fact]
        public void ReorgChain_FailsPartialValidation_Nodes_Connected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable syncer from sending blocks to miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidDuplicateCoinbase(coreNode, block)).BuildAsync();

                // Reconnect minerA to minerB.
                // This will cause the reorg chain to fail at block 8 and roll back any changes.
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }

        [Fact]
        public void ReorgChain_FailsPartialValidation_Nodes_Disconnected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disconnect minerA from miner B
                TestHelper.Disconnect(minerB, minerA);

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidDuplicateCoinbase(coreNode, block)).BuildAsync();

                // Reconnect minerA to minerB.
                // This will cause the reorg chain to fail at block 8 and roll back any changes.
                TestHelper.Connect(minerA, minerB);

                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }

        /// <summary>
        /// The chain that will be reconnected to has 4 blocks and 4 headers from fork point:
        ///
        /// 6 -> Full Block
        /// 7 -> Full Block
        /// 8 -> Full Block
        /// 9 -> Full Block
        /// 10 -> Header Only
        /// 11 -> Header Only
        /// 12 -> Header Only
        /// 13 -> Header Only
        /// </summary>
        [Fact]
        public void ReorgChain_FailsFullValidation_ChainHasBlocksAndHeadersOnly_NodesDisconnected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerC = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB and MinerC syncs with MinerA
                TestHelper.ConnectAndSync(minerA, minerB, minerC);

                // Disconnect MinerC from MinerA
                TestHelper.Disconnect(minerA, minerC);

                // MinerA continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoopMessage(() => { return (minerA.FullNode.ConsensusManager().Tip.Height == 9, minerA.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerB.FullNode.ConsensusManager().Tip.Height == 9, minerB.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerC.FullNode.ConsensusManager().Tip.Height == 5, minerC.FullNode.ConsensusManager().Tip.Height.ToString()); });

                // MinerB continues to mine to block 13 without block propogation
                TestHelper.DisableBlockPropagation(minerB, minerA);
                TestHelper.MineBlocks(minerB, 4);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 9));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 13));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerC, 5));

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerC).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

                // Reconnect MinerA to MinerC.
                TestHelper.Connect(minerA, minerC);

                // MinerC should be disconnected from MinerA
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerC));

                // This will cause the reorg chain to fail at block 8 and roll back any changes.
                TestHelper.WaitLoopMessage(() => { return (minerA.FullNode.ConsensusManager().Tip.Height == 9, minerA.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerB.FullNode.ConsensusManager().Tip.Height == 13, minerB.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerC.FullNode.ConsensusManager().Tip.Height == 10, minerC.FullNode.ConsensusManager().Tip.Height.ToString()); });
            }
        }
    }
}