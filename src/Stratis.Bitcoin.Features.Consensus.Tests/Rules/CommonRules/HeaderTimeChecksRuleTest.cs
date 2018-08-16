using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Tests.Common;
using Stratis.Bitcoin.Utilities;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class HeaderTimeChecksRuleTest
    {
        private readonly Network network;

        public HeaderTimeChecksRuleTest()
        {
            this.network = KnownNetworks.RegTest;
        }

        [Fact]
        public void ChecBlockPreviousTimestamp_ValidationFail()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            var rule = testContext.CreateRule<HeaderTimeChecksRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = TestRulesContextFactory.MineBlock(KnownNetworks.RegTest, testContext.Chain);
            context.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(context.ValidationContext.BlockToValidate.Header, context.ValidationContext.BlockToValidate.Header.GetHash(), testContext.Chain.Tip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.ValidationContext.BlockToValidate.Header.BlockTime = testContext.Chain.Tip.Header.BlockTime.AddSeconds(-1);

            ConsensusErrorException error = Assert.Throws<ConsensusErrorException>(() => rule.Run(context));
            Assert.Equal(ConsensusErrors.TimeTooOld, error.ConsensusError);
        }

        [Fact]
        public void ChecBlockFutureTimestamp_ValidationFail()
        {
            TestRulesContext testContext = TestRulesContextFactory.CreateAsync(this.network);
            var rule = testContext.CreateRule<HeaderTimeChecksRule>();

            RuleContext context = new PowRuleContext(new ValidationContext(), testContext.DateTimeProvider.GetTimeOffset());
            context.ValidationContext.BlockToValidate = TestRulesContextFactory.MineBlock(KnownNetworks.RegTest, testContext.Chain);
            context.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(context.ValidationContext.BlockToValidate.Header, context.ValidationContext.BlockToValidate.Header.GetHash(), testContext.Chain.Tip);
            context.Time = DateTimeProvider.Default.GetTimeOffset();

            // increment the bits.
            context.ValidationContext.BlockToValidate.Header.BlockTime = context.Time.AddHours(3);

            ConsensusErrorException error = Assert.Throws<ConsensusErrorException>(() => rule.Run(context));
            Assert.Equal(ConsensusErrors.TimeTooNew, error.ConsensusError);
        }
    }
}
