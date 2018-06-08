using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
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
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                        {
                            new Transaction(),
                            CreateCoinStakeTransaction(this.network, new Key(), 6, this.concurrentChain.GetBlock(5).HashBlock)
                        }
                },
                ChainedHeader = this.concurrentChain.GetBlock(4)
            };

            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(3),
                this.ruleContext.Consensus,
                true))
                .Returns(new Target(0x1f111115))
                .Verifiable();

            await this.consensusRules.RegisterRule<CalculateStakeRule>().RunAsync(this.ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull(this.ruleContext as PosRuleContext);
            Assert.Equal(BlockFlag.BLOCK_PROOF_OF_STAKE, (this.ruleContext as PosRuleContext).BlockStake.Flags);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.HashProof);
            Assert.Equal((uint)18276127, (this.ruleContext as PosRuleContext).BlockStake.StakeTime);
            Assert.Equal(this.concurrentChain.GetBlock(5).HashBlock, (this.ruleContext as PosRuleContext).BlockStake.PrevoutStake.Hash);
            Assert.Equal(new Target(0x1f111115).Difficulty, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_DoNotCheckPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction()
                    }
                },
                ChainedHeader = this.concurrentChain.GetBlock(4)
            };
            this.ruleContext.MinedBlock = true;

            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(3),
                this.ruleContext.Consensus,
                false))
                .Returns(new Target(0x1f111115))
                .Verifiable();

            await this.consensusRules.RegisterRule<CalculateStakeRule>().RunAsync(this.ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull(this.ruleContext as PosRuleContext);
            Assert.Equal(0, (int)(this.ruleContext as PosRuleContext).BlockStake.Flags);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.HashProof);
            Assert.Equal((uint)0, (this.ruleContext as PosRuleContext).BlockStake.StakeTime);
            Assert.Null((this.ruleContext as PosRuleContext).BlockStake.PrevoutStake);
            Assert.Equal(new Target(0x1f111115).Difficulty, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_ValidPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain),
                ChainedHeader = this.concurrentChain.Tip
            };
            this.ruleContext.MinedBlock = false;
            this.ruleContext.Consensus = this.network.Consensus;

            this.stakeValidator.Setup(s => s.GetNextTargetRequired(
                this.stakeChain.Object,
                this.concurrentChain.GetBlock(1),
                this.ruleContext.Consensus,
                false))
                .Returns(new Target(0x1f111115))
                .Verifiable();

            await this.consensusRules.RegisterRule<CalculateStakeRule>().RunAsync(this.ruleContext);

            this.stakeValidator.Verify();
            Assert.NotNull((this.ruleContext as PosRuleContext));
            Assert.Equal(0, (int)(this.ruleContext as PosRuleContext).BlockStake.Flags);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.StakeModifierV2);
            Assert.Equal(uint256.Zero, (this.ruleContext as PosRuleContext).BlockStake.HashProof);
            Assert.Equal((uint)0, (this.ruleContext as PosRuleContext).BlockStake.StakeTime);
            Assert.Null((this.ruleContext as PosRuleContext).BlockStake.PrevoutStake);
            Assert.Equal(new Target(0x1f111115).Difficulty, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = new Block()
                {
                    Transactions = new List<Transaction>()
                    {
                        new Transaction()
                    }
                },
                ChainedHeader = this.concurrentChain.GetBlock(4)
            };
            this.ruleContext.MinedBlock = false;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CalculateStakeRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }

        private static Transaction CreateCoinStakeTransaction(Network network, Key key, int height, uint256 prevout)
        {
            var coinStake = new Transaction();
            coinStake.Time = (uint)18276127;
            coinStake.AddInput(new TxIn(new OutPoint(prevout, 1)));
            coinStake.AddOutput(new TxOut(0, new Script()));
            coinStake.AddOutput(new TxOut(network.GetReward(height), key.ScriptPubKey));
            return coinStake;
        }
    }
}
