using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using NBitcoin;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class ConsensusRulesTest : TestConsensusRulesUnitTestBase
    {
        public ConsensusRulesTest()
        {
        }

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
            this.loggerFactory.Setup(l => l.CreateLogger(typeof(BlockHeaderRule).FullName))
                .Returns(new Mock<ILogger>().Object)
                .Verifiable();
            this.ruleRegistrations = new List<ConsensusRule> {
                new BlockSizeRule(),
                new BlockHeaderRule()
            };

            TestConsensusRules consensusRules = InitializeConsensusRules();
            
            consensusRules = consensusRules.Register(this.ruleRegistration.Object) as TestConsensusRules;

            List<ConsensusRuleDescriptor> rules = consensusRules.Rules.ToList();
            Assert.Equal(2, rules.Count);
            ConsensusRule rule = rules[0].Rule;
            Assert.Equal(typeof(TestConsensusRules), rule.Parent.GetType());
            Assert.NotNull(rule.Logger);

            rule = rules[1].Rule;
            Assert.Equal(typeof(TestConsensusRules), rule.Parent.GetType());
            Assert.NotNull(rule.Logger);

            this.loggerFactory.Verify();
        }

        [Fact]
        public async Task ValidateAsync_RuleWithoutAttributes_GetsRunAsync()
        {
            var rule = new Mock<ConsensusRule>();
            rule.Setup(r => r.RunAsync(It.Is<RuleContext>(c => c.SkipValidation == false)))
                .Returns(Task.FromResult(1))
                .Verifiable();

            this.ruleRegistrations = new List<ConsensusRule> { rule.Object };

            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.ValidateAsync(new RuleContext() { SkipValidation = false });

            rule.Verify();
        }

        [Fact]
        public async Task ValidateAsync_RuleWithValidationRuleAttribute_GetsRunAsync()
        {
            var rule = new ConsensusRuleWithValidationAttribute();
            this.ruleRegistrations = new List<ConsensusRule> { rule };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.ValidateAsync(new RuleContext() { SkipValidation = true });

            Assert.True(rule.RunCalled);
        }

        [Fact]
        public async Task ValidateAsync_RuleWithNonValidationRuleAttribute_GetsRunAsync()
        {
            var rule = new ConsensusRuleWithoutNonValidationRuleAttribute();
            this.ruleRegistrations = new List<ConsensusRule> { rule };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.ValidateAsync(new RuleContext() { SkipValidation = true });

            Assert.False(rule.RunCalled);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCannotSkipValidation_ContextCannotSkipValidation_RunsRuleAsync()
        {
            var rule = new ConsensusRuleWithValidationAttribute();
            this.ruleRegistrations = new List<ConsensusRule> { rule };
            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = false
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.AcceptBlockAsync(blockValidationContext, new ChainedHeader(this.network.GetGenesis().Header, this.network.GenesisHash, 0));

            Assert.True(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCanSkipValidation_ContextRequiresValidation_RunsRuleAsync()
        {
            var rule = new ConsensusRuleWithSkipValidationAttribute();
            this.ruleRegistrations = new List<ConsensusRule> { rule };

            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = false
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.AcceptBlockAsync(blockValidationContext, new ChainedHeader(this.network.GetGenesis().Header, this.network.GenesisHash, 0));

            Assert.True(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCannotSkipValidation_ContextCanSkipValidation_RunsRuleAsync()
        {
            var rule = new ConsensusRuleWithValidationAttribute();
            this.ruleRegistrations = new List<ConsensusRule> { rule };

            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = true
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.AcceptBlockAsync(blockValidationContext, new ChainedHeader(this.network.GetGenesis().Header, this.network.GenesisHash, 0));

            Assert.True(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_RuleCanSkipValidation_ContextCanSkipValidation_DoesNotRunRuleAsync()
        {
            var rule = new ConsensusRuleWithSkipValidationAttribute();
            this.ruleRegistrations = new List<ConsensusRule> { rule };

            var blockValidationContext = new ValidationContext()
            {
                ChainedHeader = this.concurrentChain.Tip,
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.RuleContext = new RuleContext() { SkipValidation = true };

            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.AcceptBlockAsync(blockValidationContext, new ChainedHeader(this.network.GetGenesis().Header, this.network.GenesisHash, 0));

            Assert.False(rule.RunCalled);
            Assert.Null(blockValidationContext.Error);
        }

        [Fact]
        public async Task ExecuteAsync_ConsensusErrorException_SetsConsensusErrorOnBlockValidationContextAsync()
        {
            ConsensusError consensusError = ConsensusErrors.BadBlockLength;
            var rule = new Mock<ConsensusRule>();
            rule.Setup(r => r.RunAsync(It.Is<RuleContext>(c => c.SkipValidation == false)))
                .Throws(new ConsensusErrorException(consensusError))
                .Verifiable();

            this.ruleRegistrations = new List<ConsensusRule> { rule.Object };
            var blockValidationContext = new ValidationContext()
            {
                RuleContext = new RuleContext()
                {
                    SkipValidation = false
                }
            };
            TestConsensusRules consensusRules = InitializeConsensusRules();
            consensusRules.Register(this.ruleRegistration.Object);

            await consensusRules.AcceptBlockAsync(blockValidationContext, new ChainedHeader(this.network.GetGenesis().Header, this.network.GenesisHash, 0));

            Assert.NotNull(blockValidationContext.Error);
            Assert.Equal(consensusError.Message, blockValidationContext.Error.Message);
            Assert.Equal(consensusError.Code, blockValidationContext.Error.Code);
        }

        // test extension methods
        [Fact]
        public void TryFindRule_RuleFound_ReturnsConsensusRule()
        {
            this.ruleRegistrations = new List<ConsensusRule> {
                new BlockSizeRule()
            };

            TestConsensusRules consensusRules = this.InitializeConsensusRules();
            consensusRules = consensusRules.Register(this.ruleRegistration.Object) as TestConsensusRules;

            var rule = consensusRules.Rules.TryFindRule<BlockSizeRule>();

            Assert.NotNull(rule);
            Assert.True(rule is BlockSizeRule);
        }

        [Fact]
        public void TryFindRule_RuleNotFound_ReturnsNull()
        {
            this.ruleRegistrations = new List<ConsensusRule> {
                new BlockHeaderRule()
            };

            TestConsensusRules consensusRules = this.InitializeConsensusRules();
            consensusRules = consensusRules.Register(this.ruleRegistration.Object) as TestConsensusRules;

            var rule = consensusRules.Rules.TryFindRule<BlockSizeRule>();

            Assert.Null(rule);
        }

        [Fact]
        public void FindRule_RuleFound_ReturnsConsensusRule()
        {
            this.ruleRegistrations = new List<ConsensusRule> {
                new BlockSizeRule()
            };

            TestConsensusRules consensusRules = this.InitializeConsensusRules();
            consensusRules = consensusRules.Register(this.ruleRegistration.Object) as TestConsensusRules;

            var rule = consensusRules.Rules.FindRule<BlockSizeRule>();

            Assert.NotNull(rule);
            Assert.True(rule is BlockSizeRule);
        }

        [Fact]
        public void FindRule_RuleNotFound_ThrowsException()
        {
            Assert.Throws<Exception>(() =>
            {
                this.ruleRegistrations = new List<ConsensusRule> {
                    new BlockHeaderRule()
                };

                TestConsensusRules consensusRules = this.InitializeConsensusRules();
                consensusRules = consensusRules.Register(this.ruleRegistration.Object) as TestConsensusRules;

                consensusRules.Rules.FindRule<BlockSizeRule>();
            });
        }

        [ValidationRule]
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

        [ValidationRule(CanSkipValidation = true)]
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

        [ExecutionRule]
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
}
