using FluentAssertions;
using NBitcoin;
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
            // TODO: Create fake blocks.
        }
    }
}
