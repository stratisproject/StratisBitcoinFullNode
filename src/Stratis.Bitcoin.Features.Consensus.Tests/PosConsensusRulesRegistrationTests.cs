using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    public class PosConsensusRulesRegistrationTests
    {
        private readonly IEnumerable<ConsensusRule> rules;

        public PosConsensusRulesRegistrationTests()
        {
            this.rules = new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().GetRules();
        }

        [Fact]
        public void GetRules_ForPOS_ReturnsListOfRegisteredPowRules()
        {
            this.rules.Count().Should().Be(22);

            this.rules.ElementAt(0).Should().BeOfType<BlockHeaderRule>();
            this.rules.ElementAt(1).Should().BeOfType<CalculateStakeRule>();
            this.rules.ElementAt(2).Should().BeOfType<CheckpointsRule>();
            this.rules.ElementAt(3).Should().BeOfType<AssumeValidRule>();
            this.rules.ElementAt(4).Should().BeOfType<BlockHeaderPowContextualRule>();
            this.rules.ElementAt(5).Should().BeOfType<BlockHeaderPosContextualRule>();
            this.rules.ElementAt(6).Should().BeOfType<TransactionLocktimeActivationRule>();
            this.rules.ElementAt(7).Should().BeOfType<CoinbaseHeightActivationRule>();
            this.rules.ElementAt(8).Should().BeOfType<WitnessCommitmentsRule>();
            this.rules.ElementAt(9).Should().BeOfType<BlockSizeRule>();
            this.rules.ElementAt(10).Should().BeOfType<PosBlockContextRule>();
            this.rules.ElementAt(11).Should().BeOfType<BlockMerkleRootRule>();
            this.rules.ElementAt(12).Should().BeOfType<EnsureCoinbaseRule>();
            this.rules.ElementAt(13).Should().BeOfType<CheckPowTransactionRule>();
            this.rules.ElementAt(14).Should().BeOfType<CheckPosTransactionRule>();
            this.rules.ElementAt(15).Should().BeOfType<CheckSigOpsRule>();
            this.rules.ElementAt(16).Should().BeOfType<PosFutureDriftRule>();
            this.rules.ElementAt(17).Should().BeOfType<PosCoinstakeRule>();
            this.rules.ElementAt(18).Should().BeOfType<PosBlockSignatureRule>();
            this.rules.ElementAt(19).Should().BeOfType<LoadCoinviewRule>();
            this.rules.ElementAt(20).Should().BeOfType<TransactionDuplicationActivationRule>();
            this.rules.ElementAt(21).Should().BeOfType<PosCoinviewRule>();
        }
    }
}