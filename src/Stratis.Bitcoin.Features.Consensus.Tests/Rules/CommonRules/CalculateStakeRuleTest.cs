using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateStakeRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public CalculateStakeRuleTest()
        {
            this.concurrentChain = GenerateChainWithHeight(5, this.network);
            this.consensusRules = this.InitializeConsensusRules();
        }

        [Fact]
        public async Task RunAsync_ProofOfStakeBlock_SetsStake_SetsNextWorkRequiredAsync()
        {
            Block block = this.network.CreateBlock();
            Transaction transaction = this.network.CreateTransaction();
            block.AddTransaction(transaction);
            block.AddTransaction(CreateCoinStakeTransaction(this.network, new Key(), 6, this.concurrentChain.GetBlock(5).HashBlock));
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = block,
                ChainedHeaderToValidate = this.concurrentChain.GetBlock(4)
            };

            var target = new Target(0x1f111115);
            this.ruleContext.ValidationContext.BlockToValidate.Header.Bits = target;

            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(3),
                this.network.Consensus,
                true))
                .Returns(target)
                .Verifiable();

            await this.consensusRules.RegisterRule<CheckDifficultyHybridRule>().RunAsync(this.ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull(this.ruleContext as PosRuleContext);
            Assert.Equal(BlockFlag.BLOCK_PROOF_OF_STAKE, (this.ruleContext as PosRuleContext).BlockStake.Flags);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.HashProof);
            Assert.Equal((uint)18276127, (this.ruleContext as PosRuleContext).BlockStake.StakeTime);
            Assert.Equal(this.concurrentChain.GetBlock(5).HashBlock, (this.ruleContext as PosRuleContext).BlockStake.PrevoutStake.Hash);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_ValidPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.network = KnownNetworks.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain),
                ChainedHeaderToValidate = this.concurrentChain.Tip
            };

            var target = this.ruleContext.ValidationContext.BlockToValidate.Header.Bits;

            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(1),
                this.network.Consensus,
                false))
                .Returns(target)
                .Verifiable();

            await this.consensusRules.RegisterRule<CheckDifficultyHybridRule>().RunAsync(this.ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull((this.ruleContext as PosRuleContext));
            Assert.Equal(0, (int)(this.ruleContext as PosRuleContext).BlockStake.Flags);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.HashProof);
            Assert.Equal((uint)0, (this.ruleContext as PosRuleContext).BlockStake.StakeTime);
            Assert.Null((this.ruleContext as PosRuleContext).BlockStake.PrevoutStake);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            Block block = this.network.CreateBlock();
            Transaction transaction = this.network.CreateTransaction();
            block.AddTransaction(transaction);

            this.ruleContext.ValidationContext = new ValidationContext()
            {
                BlockToValidate = block,
                ChainedHeaderToValidate = this.concurrentChain.GetBlock(4)
            };

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CheckDifficultyHybridRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }

        private static Transaction CreateCoinStakeTransaction(Network network, Key key, int height, uint256 prevout)
        {
            var coinStake = network.CreateTransaction();
            coinStake.Time = (uint)18276127;
            coinStake.AddInput(new TxIn(new OutPoint(prevout, 1)));
            coinStake.AddOutput(new TxOut(0, new Script()));
            coinStake.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
            return coinStake;
        }
    }
}