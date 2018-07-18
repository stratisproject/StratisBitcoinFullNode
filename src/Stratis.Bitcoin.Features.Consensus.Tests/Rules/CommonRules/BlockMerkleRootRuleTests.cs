using FluentAssertions;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class BlockMerkleRootRuleTests
    {
        [Fact]
        public void BlockMerkleRootRule_Cannot_Be_Skipped()
        {
            var descriptor = new ConsensusRuleDescriptor(new BlockMerkleRootRule());

            descriptor.Rule.Should().BeOfType<BlockMerkleRootRule>();
            descriptor.RuleAttributes.Should().HaveCount(1);
            descriptor.CanSkipValidation.Should().BeFalse();
        }
    }
}
