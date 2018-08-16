using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    // TODO ACTIVATION
    /*

    public class ConsensusRulesTest : TestConsensusRulesUnitTestBase
    {
        [Fact]
        public void Constructor_InitializesClass()
        {
            this.consensusSettings = new ConsensusSettings()
            {
                BlockAssumedValid = null,
                UseCheckpoints = true
            };

            this.checkpoints.Setup(c => c.GetLastCheckpointHeight())
                .Returns(15);
            this.dateTimeProvider.Setup(d => d.GetTime())
                .Returns(2);

            TestConsensusRules consensusRules = InitializeConsensusRules();

            Assert.Equal(this.network.Name, consensusRules.Network.Name);
            Assert.Equal(this.dateTimeProvider.Object.GetTime(), consensusRules.DateTimeProvider.GetTime());
            Assert.Equal(this.concurrentChain.Tip.HashBlock, consensusRules.Chain.Tip.HashBlock);
            Assert.Equal(this.nodeDeployments.GetFlags(this.concurrentChain.Tip).EnforceBIP30, consensusRules.NodeDeployments.GetFlags(this.concurrentChain.Tip).EnforceBIP30);
            Assert.True(consensusRules.ConsensusSettings.UseCheckpoints);
            Assert.Equal(15, consensusRules.Checkpoints.GetLastCheckpointHeight());
            this.loggerFactory.Verify(l => l.CreateLogger(typeof(TestConsensusRules).FullName));
        }

        [Fact]
        public void Register_WithRulesInRuleRegistration_UpdatesConsensusRule_AddsConensusRuleToRules()
        {
            this.loggerFactory.Setup(l => l.CreateLogger(typeof(BlockSizeRule).FullName))
                .Returns(new Mock<ILogger>().Object)
                .Verifiable();
            this.loggerFactory.Setup(l => l.CreateLogger(typeof(SetActivationDeploymentsPartialValidationRule).FullName))
                .Returns(new Mock<ILogger>().Object)
                .Verifiable();

            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> {
                new BlockSizeRule(),
                new SetActivationDeploymentsPartialValidationRule()
            };
            consensusRules = consensusRules.Register() as TestConsensusRules;

            List<ConsensusRule> rules = consensusRules.Rules.ToList();
            Assert.Equal(2, rules.Count);
            ConsensusRule rule = (ConsensusRule)rules[0];
            Assert.Equal(typeof(TestConsensusRules), rule.Parent.GetType());
            Assert.NotNull(rule.Logger);

            rule = (ConsensusRule)rules[1];
            Assert.Equal(typeof(TestConsensusRules), rule.Parent.GetType());
            Assert.NotNull(rule.Logger);

            this.loggerFactory.Verify();
        }

        [Fact]
        public async Task ValidateAsync_RuleWithoutAttributes_GetsRunAsync()
        {
            var rule = new Mock<ConsensusRule>();
            rule.Setup(r => r.RunAsync(It.Is<RuleContext>(c => c.SkipValidation == false)));

            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule.Object };
            Assert.Throws<ConsensusException>(() => { consensusRules.Register(); });
        }

        [Fact]
        public async Task ValidateAsync_RuleWithValidationRuleAttribute_GetsRunAsync()
        {
            var rule = new ConsensusRuleWithValidationAttribute();
            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule };
            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.True(rule.RunCalled);
        }

        [Fact]
        public async Task ValidateAsync_RuleWithNonValidationRuleAttribute_GetsRunAsync()
        {
            var rule = new ConsensusRuleWithoutNonValidationRuleAttribute();
            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule };
            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.False(rule.RunCalled);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCannotSkipValidation_ContextCannotSkipValidation_RunsRuleAsync()
        {
            var rule = new ConsensusRuleWithValidationAttribute();
            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = false
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule };
            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.True(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCanSkipValidation_ContextRequiresValidation_RunsRuleAsync()
        {
            var rule = new ConsensusRuleWithSkipValidationAttribute();

            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = false
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule };
            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.True(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCannotSkipValidation_ContextCanSkipValidation_RunsRuleAsync()
        {
            var rule = new ConsensusRuleWithValidationAttribute();

            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = true
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule };
            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.True(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCanSkipValidation_ContextCanSkipValidation_DoesNotRunRuleAsync()
        {
            var rule = new ConsensusRuleWithSkipValidationAttribute();
            var blockValidationContext = new ValidationContext()
            {
                ChainTipToExtend = this.concurrentChain.Tip,
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { rule };
            consensusRules.RuleContext = new RuleContext() { SkipValidation = true };

            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.False(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [PartialValidationRule]
        public class TestRule : ConsensusRule
        {
            public override Task RunAsync(RuleContext context)
            {
                throw new ConsensusErrorException(ConsensusErrors.BadBlockLength);
            }
        }

        [Fact]
        public async Task ExecuteAsync_ConsensusErrorException_SetsConsensusErrorOnBlockValidationContextAsync()
        {
            ConsensusError consensusError = ConsensusErrors.BadBlockLength;
            var rule = new Mock<ConsensusRule>();
            rule.Setup(r => r.RunAsync(It.Is<RuleContext>(c => c.SkipValidation == false)))
                .Throws(new ConsensusErrorException(consensusError))
                .Verifiable();

            var validationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = false
                }
            };

            TestConsensusRules consensusRules = InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { new TestRule() };
            consensusRules.Register();

            await consensusRules.PartialValidationAsync(new ValidationContext());

            Assert.NotNull(validationContext.Error);
            Assert.Equal(consensusError.Message, validationContext.Error.Message);
            Assert.Equal(consensusError.Code, validationContext.Error.Code);
        }

        // test extension methods
        [Fact]
        public void TryFindRule_RuleFound_ReturnsConsensusRule()
        {
            TestConsensusRules consensusRules = this.InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { new BlockSizeRule() }; consensusRules = consensusRules.Register() as TestConsensusRules;

            var rule = consensusRules.Rules.TryFindRule<BlockSizeRule>();

            Assert.NotNull(rule);
            Assert.True(rule is BlockSizeRule);
        }

        [Fact]
        public void TryFindRule_RuleNotFound_ReturnsNull()
        {
            TestConsensusRules consensusRules = this.InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> { new SetActivationDeploymentsPartialValidationRule() };
            consensusRules = consensusRules.Register() as TestConsensusRules;

            var rule = consensusRules.Rules.TryFindRule<BlockSizeRule>();

            Assert.Null(rule);
        }

        [Fact]
        public void FindRule_RuleFound_ReturnsConsensusRule()
        {
            TestConsensusRules consensusRules = this.InitializeConsensusRules();
            this.network.Consensus.Rules = new List<IBaseConsensusRule> {
                new BlockSizeRule()
            };
            consensusRules = consensusRules.Register() as TestConsensusRules;

            var rule = consensusRules.Rules.FindRule<BlockSizeRule>();

            Assert.NotNull(rule);
            Assert.True(rule is BlockSizeRule);
        }

        [Fact]
        public void FindRule_RuleNotFound_ThrowsException()
        {
            Assert.Throws<Exception>(() =>
            {
                TestConsensusRules consensusRules = this.InitializeConsensusRules();
                this.network.Consensus.Rules = new List<IBaseConsensusRule> {
                    new SetActivationDeploymentsPartialValidationRule()
                };
                consensusRules = consensusRules.Register() as TestConsensusRules;

                consensusRules.Rules.FindRule<BlockSizeRule>();
            });
        }

        [PartialValidationRule]
        private class ConsensusRuleWithValidationAttribute : ConsensusRule
        {
            public bool RunCalled { get; private set; }

            public ConsensusRuleWithValidationAttribute()
            {
            }

            public override Task RunAsync(RuleContext context)
            {
                this.RunCalled = true;

                return Task.FromResult(15);
            }
        }

        [PartialValidationRule(CanSkipValidation = true)]
        private class ConsensusRuleWithSkipValidationAttribute : ConsensusRule
        {
            public bool RunCalled { get; private set; }

            public ConsensusRuleWithSkipValidationAttribute()
            {
            }

            public override Task RunAsync(RuleContext context)
            {
                this.RunCalled = true;

                return Task.FromResult(15);
            }
        }

        [FullValidationRule]
        private class ConsensusRuleWithoutNonValidationRuleAttribute : ConsensusRule
        {
            public bool RunCalled { get; private set; }

            public ConsensusRuleWithoutNonValidationRuleAttribute()
            {
            }

            public override Task RunAsync(RuleContext context)
            {
                this.RunCalled = true;

                return Task.FromResult(15);
            }
        }
    }
     */
}
