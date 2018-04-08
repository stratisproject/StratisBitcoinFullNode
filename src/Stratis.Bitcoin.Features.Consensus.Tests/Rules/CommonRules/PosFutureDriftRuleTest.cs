using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class PosFutureDriftRuleTest : TestPosConsensusRulesUnitTestBase
    {
        public PosFutureDriftRuleTest() : base()
        {
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampTooNew_WithoutReducedDrift_ThrowsBlockTimestampTooFarConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                };

                var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp - 100);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(futureDriftTimestamp);
                ruleContext.BlockValidationContext.Block.Header.Time = (((uint)futureDriftTimestamp) + 128 * 60 * 60) + 1;

                var rule = this.consensusRules.RegisterRule<PosFutureDriftRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BlockTimestampTooFar.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BlockTimestampTooFar.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampTooNew_WithReducedDrift_ThrowsBlockTimestampTooFarConsensusErrorAsync()
        {
            var exception = await Assert.ThrowsAsync<ConsensusErrorException>(() =>
            {
                var ruleContext = new RuleContext()
                {
                    BlockValidationContext = new BlockValidationContext()
                    {
                        Block = new NBitcoin.Block()
                    },
                };

                var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp + 100);
                this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                    .Returns(futureDriftTimestamp);
                ruleContext.BlockValidationContext.Block.Header.Time = (uint)futureDriftTimestamp + 16;

                var rule = this.consensusRules.RegisterRule<PosFutureDriftRule>();
                return rule.RunAsync(ruleContext);
            });

            Assert.Equal(ConsensusErrors.BlockTimestampTooFar.Code, exception.ConsensusError.Code);
            Assert.Equal(ConsensusErrors.BlockTimestampTooFar.Message, exception.ConsensusError.Message);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampSameAsFutureDriftLimit_WithoutReducedDrift_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };

            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            ruleContext.BlockValidationContext.Block.Header.Time = (((uint)futureDriftTimestamp) + 128 * 60 * 60);

            var rule = this.consensusRules.RegisterRule<PosFutureDriftRule>();
            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampSameAsFutureDriftLimit_WithReducedDrift_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };

            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            ruleContext.BlockValidationContext.Block.Header.Time = (uint)futureDriftTimestamp + 15;

            var rule = this.consensusRules.RegisterRule<PosFutureDriftRule>();
            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampBelowFutureDriftLimit_WithoutReducedDrift_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };

            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp - 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            ruleContext.BlockValidationContext.Block.Header.Time = (((uint)futureDriftTimestamp) + 128 * 60 * 60) - 1;

            var rule = this.consensusRules.RegisterRule<PosFutureDriftRule>();
            await rule.RunAsync(ruleContext);
        }

        [Fact]
        public async Task RunAsync_HeaderTimestampBelowFutureDriftLimit_WithReducedDrift_DoesNotThrowExceptionAsync()
        {
            var ruleContext = new RuleContext()
            {
                BlockValidationContext = new BlockValidationContext()
                {
                    Block = new NBitcoin.Block()
                },
            };

            var futureDriftTimestamp = (PosConsensusValidator.DriftingBugFixTimestamp + 100);
            this.dateTimeProvider.Setup(d => d.GetAdjustedTimeAsUnixTimestamp())
                .Returns(futureDriftTimestamp);
            ruleContext.BlockValidationContext.Block.Header.Time = (uint)futureDriftTimestamp + 14;

            var rule = this.consensusRules.RegisterRule<PosFutureDriftRule>();
            await rule.RunAsync(ruleContext);
        }
    }
}
