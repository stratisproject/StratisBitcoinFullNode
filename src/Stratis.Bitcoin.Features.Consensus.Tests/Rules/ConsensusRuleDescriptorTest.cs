using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules
{
    public class ConsensusRuleDescriptorTest
    {
        [Fact]
        public void Constructor_ConsensusRuleWithMempoolAndExecutionAndValidationAttribute_CannotSkipValidation()
        {
            var descriptor = new ConsensusRuleDescriptor(new RuleWithValidationExecutionMempoolAttributes());

            descriptor.Rule.Should().BeOfType<RuleWithValidationExecutionMempoolAttributes>();
            descriptor.RuleAttributes.Should().HaveCount(3);
            descriptor.RuleAttributes.Should().Contain(x => x is MempoolRuleAttribute);
            descriptor.RuleAttributes.Should().Contain(x => x is ExecutionRuleAttribute);
            descriptor.RuleAttributes.Should().Contain(x => x is ValidationRuleAttribute);

            descriptor.CanSkipValidation.Should().BeFalse();
        }

        [Fact]
        public void Constructor_ConsensusRuleWithOptionalValidationAttribute_CanSkipValidation()
        {
            var descriptor = new ConsensusRuleDescriptor(new RuleWithOptionalValidationAttribute());

            descriptor.Rule.Should().BeOfType<RuleWithOptionalValidationAttribute>();
            descriptor.RuleAttributes.Should().HaveCount(1);
            descriptor.RuleAttributes.Single().Should().BeOfType<ValidationRuleAttribute>();

            descriptor.CanSkipValidation.Should().BeTrue();
        }

        [Fact]
        public void Constructor_ConsensusRuleWithoutValidationAttributeButWithOtherRuleAttribute_CannotSkipValidation()
        {
            var descriptor = new ConsensusRuleDescriptor(new RuleWithMempoolAttributeButWithoutValidationAttribute());

            descriptor.Rule.Should().BeOfType<RuleWithMempoolAttributeButWithoutValidationAttribute>();

            descriptor.RuleAttributes.Should().HaveCount(1);

            descriptor.CanSkipValidation.Should().BeFalse();
        }

        [Fact]
        public void Constructor_ConsensusRuleWithZeroRuleAttributes_CanSkipValidation()
        {
            var descriptor = new ConsensusRuleDescriptor(new RuleWithoutRuleAttributes());

            descriptor.Rule.Should().BeOfType<RuleWithoutRuleAttributes>();

            descriptor.RuleAttributes.Should().BeEmpty();
            descriptor.CanSkipValidation.Should().BeTrue();
        }

        private abstract class MyConsensusRule : ConsensusRule
        {
            public override Task RunAsync(RuleContext context)
            {
                throw new NotImplementedException();
            }
        }

        [MempoolRule]
        [ExecutionRule]
        [ValidationRule(CanSkipValidation = false)]
        private class RuleWithValidationExecutionMempoolAttributes : MyConsensusRule
        {
        }

        [ValidationRule(CanSkipValidation = true)]
        private class RuleWithOptionalValidationAttribute : MyConsensusRule
        {
        }

        [MempoolRule]
        private class RuleWithMempoolAttributeButWithoutValidationAttribute : MyConsensusRule
        {
        }

        private class RuleWithoutRuleAttributes : MyConsensusRule
        {
        }
    }
}