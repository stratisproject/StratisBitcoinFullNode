using Stratis.Bitcoin.Features.PoA.Voting.ConsensusRules;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class PoAVotingCoinbaseOutputFormatRuleTests : PoARulesTestsBase
    {
        private readonly PoAVotingCoinbaseOutputFormatRule votingFormatRule;

        public PoAVotingCoinbaseOutputFormatRuleTests()
        {
            this.votingFormatRule = new PoAVotingCoinbaseOutputFormatRule();
            this.InitRule(this.votingFormatRule);
        }

        // TODO add tests
    }
}
