using System.Threading.Tasks;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.IntegrationTests.Common.ReadyData;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Primitives;
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
        public async Task ReorgChain_AfterInitialRewind_ChainA_Extension_MinerC_DisconnectsAsync()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinRegTest();

                var minerA = builder.CreateStratisPowNode(network).WithDummyWallet().WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner);
                var minerB = builder.CreateStratisPowNode(network).NoValidation().WithDummyWallet();
                var syncer = builder.CreateStratisPowNode(network).WithDummyWallet();

                bool minerADisconnectedFromSyncer = false;

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                void interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (minerADisconnectedFromSyncer)
                        return;

                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 10)
                    {
                        // Ensure that minerA's tip has rewound to 10.
                        TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 10));
                        TestHelper.Disconnect(minerA, syncer);
                        minerADisconnectedFromSyncer = true;

                        return;
                    }
                }

                minerA.SetDisconnectInterceptor(interceptor);

                // Start the nodes.
                minerA.Start();
                minerB.Start();
                syncer.Start();

                // minerB and syncer syncs with minerA.
                TestHelper.ConnectAndSync(minerA, minerB, syncer);

                // Disconnect minerB from miner so that it can mine on its own and create a fork.
                TestHelper.Disconnect(minerA, minerB);

                // MinerA continues to mine to height 14.
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 14);

                // minerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                Assert.False(TestHelper.IsNodeConnected(minerB));
                await TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(13, (node, block) => BlockBuilder.InvalidCoinbaseReward(node, block)).BuildAsync();

                // Reconnect minerA to minerB.
                TestHelper.Connect(minerA, minerB);

                // minerB should be disconnected from minerA.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerB));

                // syncer should be disconnected from minerA (via interceptor).
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, syncer));

                // The reorg will fail at block 8 and roll back any changes.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 14));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 15));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(syncer, 14));
            }
        }

        /// <summary>
        /// In this scenario we test what happens when a node disconnected during a rewind and before it
        /// tries to connect to another chain with longer chain work.
        /// </summary>
        [Fact]
        public void ReorgChain_AfterInitialRewind_ChainB_MinerB_Disconnects()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var network = new BitcoinRegTest();

                var minerA = builder.CreateStratisPowNode(network).WithReadyBlockchainData(ReadyBlockchain.BitcoinRegTest10Miner).WithDummyWallet();
                var minerB = builder.CreateStratisPowNode(network).WithDummyWallet();
                var syncer = builder.CreateStratisPowNode(network).WithDummyWallet();

                bool minerADisconnectedFromMinerB = false;

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                void interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (minerADisconnectedFromMinerB)
                        return;

                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 10)
                    {
                        // Ensure that minerA's tips has rewound to 10.
                        TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 10));
                        TestHelper.Disconnect(minerA, minerB);
                        minerADisconnectedFromMinerB = true;

                        return;
                    }
                }

                minerA.SetDisconnectInterceptor(interceptor);

                // Start the nodes.
                minerA.Start();
                minerB.Start();
                syncer.Start();

                // MinerB/Syncer syncs with MinerA.
                TestHelper.ConnectAndSync(minerA, minerB, syncer);

                // Disable block propagation from MinerA to MinerB so that it can mine on its own and create a fork.
                TestHelper.DisableBlockPropagation(minerA, minerB);

                // MinerA continues to mine to height 14.
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 14);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 14);

                // MinerB mines 5 more blocks so that a reorg is triggered.
                TestHelper.MineBlocks(minerB, 5);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 15));

                // MinerA and Syncer should have reorged to the longer chain.
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(minerA, minerB));
                TestHelper.WaitLoop(() => TestHelper.AreNodesSynced(syncer, minerB));
            }
        }
    }
}
