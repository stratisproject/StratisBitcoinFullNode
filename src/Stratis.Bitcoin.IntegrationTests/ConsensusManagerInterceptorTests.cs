using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Primitives;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public sealed class ConsensusManagerInterceptorTests
    {
        /// <summary>
        /// In this scenario we test what happens when a node disconnected during a rewind and before it
        /// tries to connect to another chain with longer chain work.
        /// </summary>
        [Fact]
        public void ReorgChain_AfterInitialRewind_Miner_Disconnects()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(new StratisRegTest()).OverrideDateTimeProvider().WithDummyWallet();
                var minerB = builder.CreateStratisPosNode(new StratisRegTest()).OverrideDateTimeProvider().NoValidation().WithDummyWallet();
                var minerC = builder.CreateStratisPosNode(new StratisRegTest()).OverrideDateTimeProvider().WithDummyWallet();

                // Configure the interceptor to disconnect a node after a certain block has been disconnected (rewound).
                bool interceptor(ChainedHeaderBlock chainedHeaderBlock)
                {
                    if (chainedHeaderBlock.ChainedHeader.Previous.Height == 5)
                    {
                        TestHelper.Disconnect(minerA, minerC);
                        return true;
                    }

                    return false;
                }
                minerA.BlockDisconnectInterceptor(interceptor);

                // Start the nodes.
                minerA.Start();
                minerB.Start();
                minerC.Start();

                // MinerA mines 5 blocks.
                TestHelper.MineBlocks(minerA, 5);

                // MinerB and MinerC syncs with MinerA.
                TestHelper.ConnectAndSync(minerA, minerB, minerC);

                // Disconnect MinerB from MinerA so that is can mine on its own and create a fork.
                TestHelper.Disconnect(minerA, minerB);

                // MinerA continues to mine to height 9.
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 5);
                TestHelper.WaitLoop(() => minerC.FullNode.ConsensusManager().Tip.Height == 9);

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Amount(5).Invalid(8, (node, block) => BlockBuilder.InvalidCoinbaseReward(node, block)).BuildAsync();

                // Reconnect MinerA to MinerB.
                TestHelper.Connect(minerA, minerB);

                // MinerC should be disconnected from MinerA.
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnectedTo(minerA, minerC));

                // This will cause the reorg chain to fail at block 8 and roll back any changes.
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerA, 9));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerB, 10));
                TestHelper.WaitLoop(() => TestHelper.IsNodeSyncedAtHeight(minerC, 9));
            }
        }
    }
}
