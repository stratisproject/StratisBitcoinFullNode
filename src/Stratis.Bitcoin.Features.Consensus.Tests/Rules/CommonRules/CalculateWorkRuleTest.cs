using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class CalculateWorkRuleTest : TestConsensusRulesUnitTestBase
    {
        public CalculateWorkRuleTest()
        {
        }

        [Fact]
        public async Task CheckHeaderBits_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Network.RegTest);
            var rule = testContext.CreateRule<HeaderTimeChecksRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), Network.RegTest.Consensus, testContext.Chain.Tip, testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.Block = TestRulesContextFactory.MineBlock(Network.RegTest, testContext.Chain);
            context.ValidationContext.ChainedHeader = new ChainedHeader(context.ValidationContext.Block.Header, context.ValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.NextWorkRequired = context.ValidationContext.ChainedHeader.GetNextWorkRequired(Network.RegTest.Consensus);
            context.ValidationContext.Block.Header.Bits += 1;

            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.BadDiffBits, error.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_DoNotCheckPow_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.Tip;
            this.ruleContext.ValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);
            this.ruleContext.MinedBlock = true;
            this.ruleContext.Consensus = this.network.Consensus;

            await this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext);

            Assert.Equal(0.465, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_ValidPow_SetsStake_SetsNextWorkRequiredAsync()
        {
            this.network = Network.RegTest;
            this.concurrentChain = MineChainWithHeight(2, this.network);
            this.consensusRules = this.InitializeConsensusRules();

            this.ruleContext.ValidationContext.ChainedHeader = this.concurrentChain.Tip;
            this.ruleContext.ValidationContext.Block = TestRulesContextFactory.MineBlock(this.network, this.concurrentChain);
            this.ruleContext.MinedBlock = false;
            this.ruleContext.Consensus = this.network.Consensus;

            await this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext);

            Assert.Equal(0.465, this.ruleContext.NextWorkRequired.Difficulty);
        }

        [Fact]
        public async Task RunAsync_ProofOfWorkBlock_CheckPow_InValidPow_ThrowsHighHashConsensusErrorExceptionAsync()
        {
            Block block = this.network.CreateBlock();
            this.ruleContext.ValidationContext = new ValidationContext()
            {
                Block = block,
                ChainedHeader = this.concurrentChain.GetBlock(4)
            };
            this.ruleContext.MinedBlock = false;

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<CalculateWorkRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.HighHash, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetLower_ThrowsBadDiffBitsConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = 5;
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111114);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<HeaderTimeChecksRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_RequiredProofOfWorkNotMetHigher_ThrowsBadDiffBitsConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = 5;
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111116);

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<HeaderTimeChecksRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BadDiffBits, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldLower_ThrowsTimeTooOldConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = 5;
            this.ruleContext.ConsensusTip.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 0, 9));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<HeaderTimeChecksRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooOldEqual_ThrowsTimeTooOldConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = 5;
            this.ruleContext.ConsensusTip.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.Time = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 0));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<HeaderTimeChecksRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_TimeTooNew_ThrowsTimeTooNewConsensusErrorAsync()
        {
            this.ruleContext.ConsensusTipHeight = 5;
            this.ruleContext.Time = new DateTime(2016, 12, 31, 10, 0, 0);
            this.ruleContext.NextWorkRequired = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block = this.network.CreateBlock();
            this.ruleContext.ValidationContext.Block.Header.Bits = new Target(0x1f111115);
            this.ruleContext.ValidationContext.Block.Header.BlockTime = new DateTimeOffset(new DateTime(2017, 1, 1, 0, 1, 1));

            ConsensusErrorException exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<HeaderTimeChecksRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
        }

    }
}