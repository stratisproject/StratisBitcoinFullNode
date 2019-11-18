using System.Threading.Tasks;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.TestNetworks;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Primitives;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public sealed class ConsensusManagerInterceptorTests
    {
        /// <summary>
        /// In this scenario we test what happens when a node disconnected during a rewind and before it
        /// tries to connect to another chain with longer chain work but containing an invalid block.
        /// </summary>
        [Fact]
        public async Task ReorgChain_Scenario1_Async()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(new BitcoinRegTest(), "cmi-1-minerA").WithDummyWallet();
                var minerB = builder.CreateStratisPowNode(new BitcoinRegTestNoValidationRules(), "cmi-1-minerB").NoValidation().WithDummyWallet();
                var syncer = builder.CreateStratisPowNode(new BitcoinRegTest(), "cmi-1-syncer").WithDummyWallet();

                bool minerADisconnectedFromSyncer = false;

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                void interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (minerADisconnectedFromSyncer)
                        return;

                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 10)
                    {
                        // Ensure that minerA's tip has rewound to 10.
                        TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 10));
                        TestHelper.Disconnect(minerA, syncer);
                        minerADisconnectedFromSyncer = true;

                        return;
                    }
                }

                // Start minerA and mine 10 blocks. We cannot use a premade chain as it adversely affects the max tip age calculation, causing sporadic sync errors.
                minerA.Start();
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                // Start the nodes.
                minerB.Start();
                syncer.Start();

                minerA.SetDisconnectInterceptor(interceptor);

                // minerB and syncer syncs with minerA.
                TestHelper.ConnectAndSync(minerA, minerB, syncer);

                // Disconnect minerB from miner so that it can mine on its own and create a fork.
                TestHelper.Disconnect(minerA, minerB);

                // MinerA continues to mine to height 14.
                TestHelper.MineBlocks(minerA, 4);
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 14);

                // minerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                Assert.False(TestHelper.IsNodeConnected(minerB));
                await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(13, (node, block) => BlockBuilder.InvalidCoinbaseReward(node, block)).BuildAsync();

                // Reconnect minerA to minerB.
                TestHelper.ConnectNoCheck(minerA, minerB);

                // minerB should be disconnected from minerA.
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // syncer should be disconnected from minerA (via interceptor).
                TestBase.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, syncer));

                // The reorg will fail at block 8 and roll back any changes.
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 14));
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 15));
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(syncer, 14));
            }
        }

        /// <summary>
        /// In this scenario we test what happens when a node disconnected during a rewind and before it
        /// tries to connect to another chain with longer chain work.
        /// </summary>
        [Fact]
        public void ReorgChain_Scenario2()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPowNode(new BitcoinRegTest(), "cmi-2-minerA").WithDummyWallet();
                var minerB = builder.CreateStratisPowNode(new BitcoinRegTest(), "cmi-2-minerB").WithDummyWallet();
                var syncer = builder.CreateStratisPowNode(new BitcoinRegTest(), "cmi-2-syncer").WithDummyWallet();

                bool minerADisconnectedFromMinerB = false;

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                void interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (minerADisconnectedFromMinerB)
                        return;

                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 10)
                    {
                        // Ensure that minerA's tips has rewound to 10.
                        TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 10));
                        TestHelper.Disconnect(minerA, minerB);
                        minerADisconnectedFromMinerB = true;
                    }
                }

                // Start minerA and mine 10 blocks. We cannot use a premade chain as it adversely affects the max tip age calculation, causing sporadic sync errors.
                minerA.Start();
                TestHelper.MineBlocks(minerA, 10);
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 10);

                // Start the other nodes.
                minerB.Start();
                syncer.Start();

                minerA.SetDisconnectInterceptor(interceptor);

                // MinerB/Syncer syncs with MinerA.
                TestHelper.ConnectAndSync(minerA, minerB, syncer);

                // Disable block propagation from MinerA to MinerB so that it can mine on its own and create a fork.
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // MinerA continues to mine to height 14.
                TestHelper.MineBlocks(minerA, 4);
                TestBase.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestBase.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestBase.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 14);

                // MinerB mines 5 more blocks so that a reorg is triggered.
                TestHelper.MineBlocks(minerB, 5);
                TestBase.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 15));

                // MinerA and Syncer should have reorged to the longer chain.
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(minerA, minerB));
                TestBase.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
            }
        }
    }
}
