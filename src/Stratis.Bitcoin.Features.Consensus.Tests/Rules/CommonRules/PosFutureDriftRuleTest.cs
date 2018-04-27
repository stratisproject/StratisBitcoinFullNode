using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
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
            this.ruleContext.BlockValidationContext.Block = new Block();
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampTooNew_WithoutReducedDrift_ThrowsBlockTimestampTooFarConsensusErrorAsync()
        {
            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.BlockValidationContext.Block.Header.Time = ((uint)futureDriftTimestamp) + MaxFutureDriftBeforeHardFork + 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosFutureDriftRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooFar, exception.ConsensusError);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampTooNew_WithReducedDrift_ThrowsBlockTimestampTooFarConsensusErrorAsync()
        {
            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)futureDriftTimestamp + MaxFutureDriftAfterHardFork + 1;

            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() => this.consensusRules.RegisterRule<PosFutureDriftRule>().RunAsync(this.ruleContext));

            Assert.Equal(ConsensusErrors.BlockTimestampTooFar.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampSameAsFutureDriftLimit_WithoutReducedDrift_DoesNotThrowExceptionAsync()
        {
            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.BlockValidationContext.Block.Header.Time = ((uint)futureDriftTimestamp) + MaxFutureDriftBeforeHardFork;

            await this.consensusRules.RegisterRule<PosFutureDriftRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampSameAsFutureDriftLimit_WithReducedDrift_DoesNotThrowExceptionAsync()
        {
            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)futureDriftTimestamp + MaxFutureDriftAfterHardFork;

            await this.consensusRules.RegisterRule<PosFutureDriftRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampBelowFutureDriftLimit_WithoutReducedDrift_DoesNotThrowExceptionAsync()
        {
            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.BlockValidationContext.Block.Header.Time = ((uint)futureDriftTimestamp) + MaxFutureDriftBeforeHardFork - 1;

            await this.consensusRules.RegisterRule<PosFutureDriftRule>().RunAsync(this.ruleContext);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampBelowFutureDriftLimit_WithReducedDrift_DoesNotThrowExceptionAsync()
        {
            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            this.ruleContext.BlockValidationContext.Block.Header.Time = (uint)futureDriftTimestamp + MaxFutureDriftAfterHardFork - 1;

            await this.consensusRules.RegisterRule<PosFutureDriftRule>().RunAsync(this.ruleContext);
        }
    }
}
