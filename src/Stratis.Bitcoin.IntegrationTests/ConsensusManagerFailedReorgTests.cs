using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerFailedReorgTests
    {
        private readonly Network posNetwork;
        private readonly Network posNetworkWithNoValidation;

        public ConsensusManagerFailedReorgTests()
        {
            this.posNetwork = new StratisRegTest();
            this.posNetworkWithNoValidation = new StratisRegTest();
        }

        [Fact]
        public void ConsensusManager_FailedReorg_Reconnect_OldChain_Nodes_Connected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(this.posNetworkWithNoValidation).NotInIBD().NoValidation().WithWallet().Start();

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
                TestHelper.BuildBlocks.OnNode(minerB).Valid(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

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

        [Fact]
        public void ConsensusManager_FailedReorg_Reconnect_OldChain_Nodes_Disconnected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(this.posNetworkWithNoValidation).NotInIBD().NoValidation().WithWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);

                // Disconnect minerA from miner B
                TestHelper.Disconnect(minerA, minerB);

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Valid(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

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
        public void ConsensusManager_FailedReorg_Reconnect_OldChain_FromSecondMiner_Disconnected()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();
                var syncer = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(this.posNetworkWithNoValidation).NotInIBD().NoValidation().WithWallet().Start();

                // MinerA mines 5 blocks
                TestHelper.MineBlocks(minerA, 5);

                // MinerB and Syncer syncs with MinerA
                TestHelper.ConnectAndSync(minerB, minerA);
                TestHelper.ConnectAndSync(syncer, minerA);

                // Disconnect minerB from miner A and syncer
                TestHelper.Disconnect(minerA, minerB);
                TestHelper.Disconnect(syncer, minerB);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(minerB));

                // Miner A continues to mine to height 9
                TestHelper.MineBlocks(minerA, 4);
                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 9);
                TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 5);
                TestHelper.WaitLoop(() => syncer.FullNode.ConsensusManager().Tip.Height == 9);

                // Disconnect minerA from syncer
                TestHelper.Disconnect(minerA, syncer);
                TestHelper.WaitLoop(() => !TestHelper.IsNodeConnected(minerA));

                // MinerB mines 5 more blocks:
                // Block 6,7,9,10 = valid
                // Block 8 = invalid
                TestHelper.BuildBlocks.OnNode(minerB).Valid(5).Invalid(8, (coreNode, block) => BlockBuilder.InvalidCoinbaseReward(coreNode, block)).BuildAsync();

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
    }
}