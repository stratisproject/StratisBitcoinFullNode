using NBitcoin;
using Stratis.Bitcoin.IntegrationTests.Common;
using Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class ConsensusManagerFailedBlockCauseReorgTests
    {
        private readonly Network posNetwork;

        public ConsensusManagerFailedBlockCauseReorgTests()
        {
            this.posNetwork = new StratisRegTest();
        }

        [Fact]
        public void ConsensusManager_Reorg_ConnectChain_Contains_BadBlock_InTheMiddle_OfChain()
        {
            using (var builder = NodeBuilder.Create(this))
            {
                var minerA = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().Start();
                var minerB = builder.CreateStratisPosNode(this.posNetwork).NotInIBD().WithWallet().NoValidation().Start();

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

                TestHelper.WaitLoop(() => minerA.FullNode.ConsensusManager().Tip.Height == 12);
                //TestHelper.WaitLoop(() => minerB.FullNode.ConsensusManager().Tip.Height == 10);
            }
        }
    }
}