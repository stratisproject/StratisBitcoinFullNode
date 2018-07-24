using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockHeaderPowContextualRuleTest
    {
        [Fact]
        public async Task CheckHeaderBits_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Networks.RegTest);
            var rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            RuleContext context = new PowRuleContext(new ValidationContext (), Networks.RegTest.Consensus, testContext.Chain.Tip, testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.Block = TestRulesContextFactory.MineBlock(Networks.RegTest, testContext.Chain);
            context.ValidationContext.ChainedHeader = new ChainedHeader(context.ValidationContext.Block.Header, context.ValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.NextWorkRequired = context.ValidationContext.ChainedHeader.GetNextWorkRequired(Networks.RegTest.Consensus);
            context.ValidationContext.Block.Header.Bits += 1;

            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.BadDiffBits, error.ConsensusError);
        }

        [Fact]
        public async Task ChecBlockPreviousTimestamp_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Networks.RegTest);
            var rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), Networks.RegTest.Consensus, testContext.Chain.Tip, testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.Block = TestRulesContextFactory.MineBlock(Networks.RegTest, testContext.Chain);
            context.ValidationContext.ChainedHeader = new ChainedHeader(context.ValidationContext.Block.Header, context.ValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.NextWorkRequired = context.ValidationContext.ChainedHeader.GetNextWorkRequired(Networks.RegTest.Consensus);
            context.ValidationContext.Block.Header.BlockTime = context.ConsensusTip.Header.BlockTime.AddSeconds(-1);

            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.TimeTooOld, error.ConsensusError);
        }

        [Fact]
        public async Task ChecBlockFutureTimestamp_ValidationFailAsync()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(Networks.RegTest);
            var rule = testContext.CreateRule<BlockHeaderPowContextualRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), Networks.RegTest.Consensus, testContext.Chain.Tip, testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.Block = TestRulesContextFactory.MineBlock(Networks.RegTest, testContext.Chain);
            context.ValidationContext.ChainedHeader = new ChainedHeader(context.ValidationContext.Block.Header, context.ValidationContext.Block.Header.GetHash(), context.ConsensusTip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.NextWorkRequired = context.ValidationContext.ChainedHeader.GetNextWorkRequired(Networks.RegTest.Consensus);
            context.ValidationContext.Block.Header.BlockTime = context.Time.AddHours(3);

            ConsensusErrorException error = await Assert.ThrowsAsync<ConsensusErrorException>(async () => await rule.RunAsync(context));
            Assert.Equal(ConsensusErrors.TimeTooNew, error.ConsensusError);
        }
    }
}
