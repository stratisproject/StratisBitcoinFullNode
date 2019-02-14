using System;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.Extensions;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class HeaderTimeChecksPoARuleTests : PoATestsBase
    {
        private readonly HeaderTimeChecksPoARule timeChecksRule;

        public HeaderTimeChecksPoARuleTests()
        {
            this.timeChecksRule = new HeaderTimeChecksPoARule();
            this.InitRule(this.timeChecksRule);
        }

        [Fact]
        public void EnsureTimestampOfNextBlockIsGreaterThanPrevBlock()
        {
            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, DateTimeOffset.Now);

            ChainedHeader prevHeader = this.currentHeader.Previous;

            // New block has smaller timestamp.
            this.currentHeader.Header.Time = this.consensusOptions.TargetSpacingSeconds;
            prevHeader.Header.Time = this.currentHeader.Header.Time + this.consensusOptions.TargetSpacingSeconds;

            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            try
            {
                this.timeChecksRule.Run(ruleContext);
            }
            catch (ConsensusErrorException exception)
            {
                Assert.Equal(ConsensusErrors.TimeTooOld, exception.ConsensusError);
            }

            // New block has equal timestamp.
            prevHeader.Header.Time = this.currentHeader.Header.Time;
            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            // New block has greater timestamp.
            prevHeader.Header.Time = this.currentHeader.Header.Time - this.consensusOptions.TargetSpacingSeconds;
            this.timeChecksRule.Run(ruleContext);
        }

        [Fact]
        public void EnsureTimestampIsNotTooNew()
        {
            long timestamp = new DateTimeProvider().GetUtcNow().ToUnixTimestamp() / this.consensusOptions.TargetSpacingSeconds * this.consensusOptions.TargetSpacingSeconds;
            DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(timestamp);

            var provider = new Mock<IDateTimeProvider>();
            provider.Setup(x => x.GetAdjustedTimeAsUnixTimestamp()).Returns(timestamp);

            this.rulesEngine = new PoAConsensusRuleEngine(this.network, this.loggerFactory, provider.Object, this.chain,
                new NodeDeployments(this.network, this.chain), this.consensusSettings, new Checkpoints(this.network, this.consensusSettings), new Mock<ICoinView>().Object,
                new ChainState(), new InvalidBlockHashStore(provider.Object), new NodeStats(provider.Object), this.slotsManager, this.poaHeaderValidator);

            this.timeChecksRule.Parent = this.rulesEngine;
            this.timeChecksRule.Initialize();

            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, time);

            ChainedHeader prevHeader = this.currentHeader.Previous;

            prevHeader.Header.Time = (uint)time.ToUnixTimeSeconds();

            var validFutureDriftOffset = HeaderTimeChecksPoARule.MaxFutureDriftSeconds / this.consensusOptions.TargetSpacingSeconds * this.consensusOptions.TargetSpacingSeconds;
            this.currentHeader.Header.Time = prevHeader.Header.Time + validFutureDriftOffset;
            this.timeChecksRule.Run(ruleContext);

            this.currentHeader.Header.Time = prevHeader.Header.Time + validFutureDriftOffset + this.consensusOptions.TargetSpacingSeconds;
            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            try
            {
                this.timeChecksRule.Run(ruleContext);
            }
            catch (ConsensusErrorException exception)
            {
                Assert.Equal(ConsensusErrors.TimeTooNew, exception.ConsensusError);
            }
        }

        [Fact]
        public void EnsureTimestampDivisibleByTargetSpacing()
        {
            DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(new DateTimeProvider().GetUtcNow().ToUnixTimestamp() / this.consensusOptions.TargetSpacingSeconds * this.consensusOptions.TargetSpacingSeconds);

            ChainedHeader prevHeader = this.currentHeader.Previous;

            var validationContext = new ValidationContext() { ChainedHeaderToValidate = this.currentHeader };
            var ruleContext = new RuleContext(validationContext, time);

            // New block has smaller timestamp.
            prevHeader.Header.Time = (uint)time.ToUnixTimeSeconds();

            this.currentHeader.Header.Time = prevHeader.Header.Time + this.consensusOptions.TargetSpacingSeconds;
            this.timeChecksRule.Run(ruleContext);

            this.currentHeader.Header.Time = prevHeader.Header.Time + this.consensusOptions.TargetSpacingSeconds - 1;

            Assert.Throws<ConsensusErrorException>(() => this.timeChecksRule.Run(ruleContext));

            try
            {
                this.timeChecksRule.Run(ruleContext);
            }
            catch (ConsensusErrorException exception)
            {
                Assert.Equal(PoAConsensusErrors.InvalidHeaderTimestamp, exception.ConsensusError);
            }
        }
    }
}
