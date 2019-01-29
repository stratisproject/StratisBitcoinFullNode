using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.PoA.Voting;
using Stratis.Bitcoin.Features.PoA.Voting.ConsensusRules;
using Xunit;

namespace Stratis.Bitcoin.Features.PoA.Tests.Rules
{
    public class PoAVotingCoinbaseOutputFormatRuleTests : PoARulesTestsBase
    {
        private readonly PoAVotingCoinbaseOutputFormatRule votingFormatRule;

        public PoAVotingCoinbaseOutputFormatRuleTests()
        {
            this.votingFormatRule = new PoAVotingCoinbaseOutputFormatRule();
            this.votingFormatRule.Parent = this.rulesEngine;
            this.votingFormatRule.Logger = this.loggerFactory.CreateLogger(this.votingFormatRule.GetType().FullName);
            this.votingFormatRule.Initialize();
        }

        // TODO add tests
    }
}
