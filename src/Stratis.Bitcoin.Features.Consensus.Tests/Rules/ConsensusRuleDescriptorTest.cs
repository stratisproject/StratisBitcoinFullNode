using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class ConsensusRuleDescriptorTest
    {
        [Fact]
        public void Constructor_ConsensusRuleWithValidationAttribute_InitializesClass()
        {
            var rule = new TestConsensusRuleWithValidationAttribute("myRule");

            var descriptor = new ConsensusRuleDescriptor(rule);

            Assert.True(descriptor.Rule is TestConsensusRuleWithValidationAttribute);
            Assert.Equal("myRule", (descriptor.Rule as TestConsensusRuleWithValidationAttribute).RuleName);

            Assert.Equal(3, descriptor.Attributes.Count);
            Assert.True(descriptor.Attributes[0] is MempoolRuleAttribute);
            Assert.True(descriptor.Attributes[1] is ExecutionRuleAttribute);
            Assert.True(descriptor.Attributes[2] is ValidationRuleAttribute);

            Assert.False(descriptor.CanSkipValidation);
        }

        [Fact]
        public void Constructor_ConsensusRuleWithOptionalValidationAttribute_InitializesClass()
        {
            var rule = new TestConsensusRuleWithOptionalValidationAttribute("myRule");

            var descriptor = new ConsensusRuleDescriptor(rule);

            Assert.True(descriptor.Rule is TestConsensusRuleWithOptionalValidationAttribute);
            Assert.Equal("myRule", (descriptor.Rule as TestConsensusRuleWithOptionalValidationAttribute).RuleName);

            Assert.Equal(3, descriptor.Attributes.Count);
            Assert.True(descriptor.Attributes[0] is MempoolRuleAttribute);
            Assert.True(descriptor.Attributes[1] is ExecutionRuleAttribute);
            Assert.True(descriptor.Attributes[2] is ValidationRuleAttribute);

            Assert.True(descriptor.CanSkipValidation);
        }

        [Fact]
        public void Constructor_ConsensusRuleWithoutValidationAttribute_InitializesClass()
        {
            var rule = new TestConsensusRuleWithoutValidationAttribute("myRule");

            var descriptor = new ConsensusRuleDescriptor(rule);

            Assert.True(descriptor.Rule is TestConsensusRuleWithoutValidationAttribute);
            Assert.Equal("myRule", (descriptor.Rule as TestConsensusRuleWithoutValidationAttribute).RuleName);

            Assert.Equal(2, descriptor.Attributes.Count);
            Assert.True(descriptor.Attributes[0] is MempoolRuleAttribute);
            Assert.True(descriptor.Attributes[1] is ExecutionRuleAttribute);

            Assert.True(descriptor.CanSkipValidation);
        }

        [MempoolRule]
        [ExecutionRule]
        [ValidationRule(CanSkipValidation = false)]
        private class TestConsensusRuleWithValidationAttribute : ConsensusRule
        {
            public string RuleName { get; }

            public TestConsensusRuleWithValidationAttribute(string ruleName) : base()
            {
                this.RuleName = ruleName;
            }

            public override Task RunAsync(RuleContext context)
            {
                throw new NotImplementedException();
            }
        }

        [MempoolRule]
        [ExecutionRule]
        [ValidationRule(CanSkipValidation = true)]
        private class TestConsensusRuleWithOptionalValidationAttribute : ConsensusRule
        {
            public string RuleName { get; }

            public TestConsensusRuleWithOptionalValidationAttribute(string ruleName) : base()
            {
                this.RuleName = ruleName;
            }

            public override Task RunAsync(RuleContext context)
            {
                throw new NotImplementedException();
            }
        }

        [MempoolRule]
        [ExecutionRule]
        private class TestConsensusRuleWithoutValidationAttribute : ConsensusRule
        {
            public string RuleName { get; }

            public TestConsensusRuleWithoutValidationAttribute(string ruleName) : base()
            {
                this.RuleName = ruleName;
            }

            public override Task RunAsync(RuleContext context)
            {
                throw new NotImplementedException();
            }
        }
    }
}
