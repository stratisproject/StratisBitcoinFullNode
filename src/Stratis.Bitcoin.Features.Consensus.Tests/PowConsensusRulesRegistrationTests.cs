using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PowConsensusRulesRegistrationTests
    {
        private readonly IEnumerable<ConsensusRule> rules;

        public PowConsensusRulesRegistrationTests()
        {
            this.rules = new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().GetRules();
        }

        [Fact]
        public void GetRules_ForPOW_ReturnsListOfRegisteredPowRules()
        {
            this.rules.Should().HaveCount(16);

            this.rules.ElementAt(0).Should().BeOfType<BlockHeaderRule>();
            this.rules.ElementAt(1).Should().BeOfType<CalculateWorkRule>();
            this.rules.ElementAt(2).Should().BeOfType<CheckpointsRule>();
            this.rules.ElementAt(3).Should().BeOfType<AssumeValidRule>();
            this.rules.ElementAt(4).Should().BeOfType<BlockHeaderPowContextualRule>();
            this.rules.ElementAt(5).Should().BeOfType<TransactionLocktimeActivationRule>();
            this.rules.ElementAt(6).Should().BeOfType<CoinbaseHeightActivationRule>();
            this.rules.ElementAt(7).Should().BeOfType<WitnessCommitmentsRule>();
            this.rules.ElementAt(8).Should().BeOfType<BlockSizeRule>();
            this.rules.ElementAt(9).Should().BeOfType<BlockMerkleRootRule>();
            this.rules.ElementAt(10).Should().BeOfType<EnsureCoinbaseRule>();
            this.rules.ElementAt(11).Should().BeOfType<CheckPowTransactionRule>();
            this.rules.ElementAt(12).Should().BeOfType<CheckSigOpsRule>();
            this.rules.ElementAt(13).Should().BeOfType<LoadCoinviewRule>();
            this.rules.ElementAt(14).Should().BeOfType<TransactionDuplicationActivationRule>();
            this.rules.ElementAt(15).Should().BeOfType<PowCoinviewRule>();
        }
    }
}