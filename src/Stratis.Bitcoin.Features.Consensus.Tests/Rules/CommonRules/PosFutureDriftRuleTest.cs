using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosFutureDriftRuleTest : TestPosConsensusRulesUnitTestBase
    {
        private const int MaxFutureDriftBeforeHardFork = 128 * 60 * 60;
        private const int MaxFutureDriftAfterHardFork = 15;

        public PosFutureDriftRuleTest()
        {
            var header = this.network.Consensus.ConsensusFactory.CreateBlockHeader();

            this.ruleContext.ValidationContext.ChainedHeaderToValidate = new ChainedHeader(header, header.GetHash(), 0);
        }

        [Fact]
        public void RunAsync_HeaderTimestampTooNew_WithoutReducedDrift_ThrowsBlockTimestampTooFarConsensusErrorAsync()
        {
            long futureDriftTimestamp = (StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = ((uint)futureDriftTimestamp) + MaxFutureDriftBeforeHardFork + 1;

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosFutureDriftRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooFar, exception.ConsensusError);
        }

        [Fact]
        public void RunAsync_HeaderTimestampTooNew_WithReducedDrift_ThrowsBlockTimestampTooFarConsensusError()
        {
            long futureDriftTimestamp = (StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = (uint)futureDriftTimestamp + MaxFutureDriftAfterHardFork + 1;

            ConsensusErrorException exception = Assert.Throws<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosFutureDriftRule>().Run(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooFar.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public void RunAsync_HeaderTimestampSameAsFutureDriftLimit_WithoutReducedDrift_DoesNotThrowExceptionAsync()
        {
            long futureDriftTimestamp = (StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = ((uint)futureDriftTimestamp) + MaxFutureDriftBeforeHardFork;

            this.consensusRules.RegisterRule<StratisBugFixPosFutureDriftRule>().Run(this.ruleContext);
        }

        [Fact]
        public void RunAsync_HeaderTimestampSameAsFutureDriftLimit_WithReducedDrift_DoesNotThrowException()
        {
            long futureDriftTimestamp = (StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = (uint)futureDriftTimestamp + MaxFutureDriftAfterHardFork;

            this.consensusRules.RegisterRule<StratisBugFixPosFutureDriftRule>().Run(this.ruleContext);
        }

        [Fact]
        public void RunAsync_HeaderTimestampBelowFutureDriftLimit_WithoutReducedDrift_DoesNotThrowException()
        {
            long futureDriftTimestamp = (StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = ((uint)futureDriftTimestamp) + MaxFutureDriftBeforeHardFork - 1;

            this.consensusRules.RegisterRule<StratisBugFixPosFutureDriftRule>().Run(this.ruleContext);
        }

        [Fact]
        public void RunAsync_HeaderTimestampBelowFutureDriftLimit_WithReducedDrift_DoesNotThrowException()
        {
            long futureDriftTimestamp = (StratisBugFixPosFutureDriftRule.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.ValidationContext.ChainedHeaderToValidate.Header.Time = (uint)futureDriftTimestamp + MaxFutureDriftAfterHardFork - 1;

            this.consensusRules.RegisterRule<StratisBugFixPosFutureDriftRule>().Run(this.ruleContext);
        }
    }
}
