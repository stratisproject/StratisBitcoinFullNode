using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.IntegrationTests.Common.TestNetworks;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Primitives;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerFailedReorgTests
    {
        private readonly Network powNetwork;

        public ConsensusManagerFailedReorgTests()
        {
            this.powNetwork = new BitcoinRegTest();
        }

        [Fact]
        public async Task ReorgChain_FailsFullValidation_Reconnect_OldChain_Nodes_ConnectedAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var bitcoinNoValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner);
                var minerB = builder.CreateStratisPowNode(bitcoinNoValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                ChainedHeader minerBChainTip = null;
                bool interceptorsEnabled = false;
                bool minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = false;
                bool minerA_IsConnecting_To_MinerBChain = false;
                bool minerA_Disconnected_MinerBsChain = false;
                bool minerA_Reconnected_Its_OwnChain = false;

                // Configure the interceptor to intercept when Miner A connects Miner B's chain.
                void interceptorConnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!interceptorsEnabled)
                        return;

                    if (!minerA_IsConnecting_To_MinerBChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 12)
                            minerA_IsConnecting_To_MinerBChain = minerA.FullNode.ConsensusManager().Tip.HashBlock == minerBChainTip.GetAncestor(12).HashBlock;

                        return;
                    }

                    if (!minerA_Reconnected_Its_OwnChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 14)
                            minerA_Reconnected_Its_OwnChain = true;

                        return;
                    }
                }

                // Configure the interceptor to intercept when Miner A disconnects Miner B's chain after the reorg.
                void interceptorDisconnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!interceptorsEnabled)
                        return;

                    if (!minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = true;

                        return;
                    }

                    if (!minerA_Disconnected_MinerBsChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_MinerBsChain = true;

                        return;
                    }
                }

                minerA.Start();
                minerA.SetConnectInterceptor(interceptorConnect);
                minerA.SetDisconnectInterceptor(interceptorDisconnect);

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable Miner A from sending blocks to Miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 14
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                Assert.Equal(10, minerB.FullNode.ConsensusManager().Tip.Height);

                // Enable the interceptors so that they are active during the reorg.
                interceptorsEnabled = true;

                // Miner B mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                minerBChainTip = await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(13, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();
                Assert.Equal(15, minerBChainTip.Height);
                Assert.Equal(15, minerB.FullNode.ConsensusManager().Tip.Height);

                // Wait until Miner A disconnected its own chain so that it can connect to
                // Miner B's longer chain.
                TestHelper.WaitLoop(() => minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain);

                // Wait until Miner A has connected Miner B's chain (but failed)
                TestHelper.WaitLoop(() => minerA_IsConnecting_To_MinerBChain);

                // Wait until Miner A has disconnected Miner B's invalid chain.
                TestHelper.WaitLoop(() => minerA_Disconnected_MinerBsChain);

                // Wait until Miner A has reconnected its own chain.
                TestHelper.WaitLoop(() => minerA_Reconnected_Its_OwnChain);
            }
        }

        [Fact]
        public async Task ReorgChain_FailsFullValidation_Reconnect_OldChain_Nodes_DisconnectedAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var bitcoinNoValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(bitcoinNoValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable Miner A from sending blocks to Miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 14
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                Assert.Equal(10, minerB.FullNode.ConsensusManager().Tip.Height);

                // Disable Miner B from sending blocks to miner A
                TestHelper.DisableBlockPropagation(minerB, minerA);

                // Miner B mines 5 more blocks [Block 6,7,9,10 = valid, Block 8 = invalid]
                var minerBChainTip = await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(13, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();
                Assert.Equal(15, minerBChainTip.Height);
                Assert.Equal(15, minerB.FullNode.ConsensusManager().Tip.Height);

                TestHelper.EnableBlockPropagation(minerB, minerA);

                bool minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = false;
                bool minerA_IsConnecting_To_MinerBChain = false;
                bool minerA_Disconnected_MinerBsChain = false;
                bool minerA_Reconnected_Its_OwnChain = false;

                // Configure the interceptor to intercept when Miner A connects Miner B's chain.
                void interceptorConnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!minerA_IsConnecting_To_MinerBChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 12)
                            minerA_IsConnecting_To_MinerBChain = minerA.FullNode.ConsensusManager().Tip.HashBlock == minerBChainTip.GetAncestor(12).HashBlock;

                        return;
                    }

                    if (!minerA_Reconnected_Its_OwnChain)
                    {
                        if (chainedHeaderBlock.ChainedHeader.Height == 14)
                            minerA_Reconnected_Its_OwnChain = true;

                        return;
                    }
                }

                // Configure the interceptor to intercept when Miner A disconnects Miner B's chain after the reorg.
                void interceptorDisconnect(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (!minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain = true;

                        return;
                    }
                    else

                    if (!minerA_Disconnected_MinerBsChain)
                    {
                        if (minerA.FullNode.ConsensusManager().Tip.Height == 10)
                            minerA_Disconnected_MinerBsChain = true;

                        return;
                    }
                }

                minerA.Restart();
                minerA.SetConnectInterceptor(interceptorConnect);
                minerA.SetDisconnectInterceptor(interceptorDisconnect);

                TestHelper.ConnectNoCheck(minerA, minerB);

                // Wait until Miner A disconnected its own chain so that it can connect to
                // Miner B's longer chain.
                TestHelper.WaitLoop(() => minerA_Disconnected_ItsOwnChain_ToConnectTo_MinerBs_LongerChain);

                // Wait until Miner A has connected Miner B's chain (but failed)
                TestHelper.WaitLoop(() => minerA_IsConnecting_To_MinerBChain);

                // Wait until Miner A has disconnected Miner B's invalid chain.
                TestHelper.WaitLoop(() => minerA_Disconnected_MinerBsChain);

                // Wait until Miner A has reconnected its own chain.
                TestHelper.WaitLoop(() => minerA_Reconnected_Its_OwnChain);
            }
        }

        [Fact]
        public async Task ReorgChain_FailsFullValidation_Reconnect_OldChain_FromSecondMiner_DisconnectedAsync()
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
                await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

                // Reconnect syncer to minerB causing the following to happen:
                // Reorg from blocks 9 to 5.
                // Connect blocks 5 to 10
                // Block 8 fails.
                // Reorg from 7 to 5
                // Reconnect blocks 6 to 9
                TestHelper.ConnectNoCheck(syncer, minerB);

                TestHelper.AreNodesSynced(minerA, syncer);

                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 9);
            }
        }

        [Fact]
        public async Task ReorgChain_FailsPartialValidation_Nodes_ConnectedAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disable Miner A from sending blocks to Miner B
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // Miner A continues to mine to height 14
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);

                // Miner B mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                var minerBTip = await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(14, (coreNode, block) => BlockBuilder.InvalidDuplicateCoinbase(coreNode, block)).BuildAsync();

                // MinerA will disconnect MinerB
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // Ensure Miner A and Miner B remains on their respective heights.
                var badBlockOnMinerBChain = minerBTip.GetAncestor(14);
                Assert.Null(minerA.FullNode.ConsensusManager().Tip.FindAncestorOrSelf(badBlockOnMinerBChain));
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 15);
            }
        }

        [Fact]
        public async Task ReorgChain_FailsPartialValidation_Nodes_DisconnectedAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // Miner B syncs with Miner A
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disconnect Miner A from Miner B
                TestHelper.Disconnect(minerB, minerA);

                // Miner A continues to mine to height 14
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);

                // Miner B mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                var minerBTip = await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(14, (coreNode, block) => BlockBuilder.InvalidDuplicateCoinbase(coreNode, block)).BuildAsync();

                // Reconnect Miner A to Miner B.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // Miner A will disconnect Miner B
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // Ensure Miner A and Miner B remains on their respective heights.
                var badBlockOnMinerBChain = minerBTip.GetAncestor(14);
                Assert.Null(minerA.FullNode.ConsensusManager().Tip.FindAncestorOrSelf(badBlockOnMinerBChain));
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 15);

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
        public async Task ReorgChain_FailsFullValidation_ChainHasBlocksAndHeadersOnly_NodesDisconnectedAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var noValidationRulesNetwork = new BitcoinRegTestNoValidationRules();

                var minerA = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).Start();
                var minerB = builder.CreateStratisPowNode(this.powNetwork).WithDummyWallet().Start();
                var minerC = builder.CreateStratisPowNode(noValidationRulesNetwork).NoValidation().WithDummyWallet().Start();

                // MinerB and MinerC syncs with MinerA
                TestHelper.ConnectAndSync(minerA, minerB, minerC);

                // Disconnect MinerC from MinerA
                TestHelper.Disconnect(minerA, minerC);

                // MinerA continues to mine to height 14
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoopMessage(() => { return (minerA.FullNode.ConsensusManager().Tip.Height == 14, minerA.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerB.FullNode.ConsensusManager().Tip.Height == 14, minerB.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerC.FullNode.ConsensusManager().Tip.Height == 10, minerC.FullNode.ConsensusManager().Tip.Height.ToString()); });

                // MinerB continues to mine to block 13 without block propogation
                TestHelper.DisableBlockPropagation(minerB, minerA);
                TestHelper.MineBlocks(minerB, 4);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 14));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 18));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerC, 10));

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                await TestHelper.BuildBlocks.OnNode(minerC).Amount(5).Invalid(13, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

                // Reconnect MinerA to MinerC.
                TestHelper.ConnectNoCheck(minerA, minerC);

                // MinerC should be disconnected from MinerA
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerC));

                // This will cause the reorg chain to fail at block 8 and roll back any changes.
                TestHelper.WaitLoopMessage(() => { return (minerA.FullNode.ConsensusManager().Tip.Height == 14, minerA.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerB.FullNode.ConsensusManager().Tip.Height == 18, minerB.FullNode.ConsensusManager().Tip.Height.ToString()); });
                TestHelper.WaitLoopMessage(() => { return (minerC.FullNode.ConsensusManager().Tip.Height == 15, minerC.FullNode.ConsensusManager().Tip.Height.ToString()); });
            }
        }
    }
}