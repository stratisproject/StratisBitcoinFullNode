using System.Collections.Generic;
using FluentAssertions;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules;
using Stratis.Bitcoin.Networks;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class ConsensusRulesRegistrationTest
    {
        [Fact]
        public void BitcoinConsensusRulesRegistrationTest()
        {
            Network network = new BitcoinTest();
            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(network.Consensus);

            List<IHeaderValidationConsensusRule> headerValidationRules = network.Consensus.HeaderValidationRules;

            headerValidationRules.Count.Should().Be(4);

            headerValidationRules[0].Should().BeOfType<HeaderTimeChecksRule>();
            headerValidationRules[1].Should().BeOfType<CheckDifficultyPowRule>();
            headerValidationRules[2].Should().BeOfType<BitcoinActivationRule>();
            headerValidationRules[3].Should().BeOfType<BitcoinHeaderVersionRule>();

            List<IIntegrityValidationConsensusRule> integrityValidationRules = network.Consensus.IntegrityValidationRules;

            integrityValidationRules.Count.Should().Be(1);
            integrityValidationRules[0].Should().BeOfType<BlockMerkleRootRule>();

            List<IPartialValidationConsensusRule> partialValidationRules = network.Consensus.PartialValidationRules;

            partialValidationRules.Count.Should().Be(8);

            partialValidationRules[0].Should().BeOfType<SetActivationDeploymentsPartialValidationRule>();
            partialValidationRules[1].Should().BeOfType<TransactionLocktimeActivationRule>();
            partialValidationRules[2].Should().BeOfType<CoinbaseHeightActivationRule>();
            partialValidationRules[3].Should().BeOfType<WitnessCommitmentsRule>();
            partialValidationRules[4].Should().BeOfType<BlockSizeRule>();
            partialValidationRules[5].Should().BeOfType<EnsureCoinbaseRule>();
            partialValidationRules[6].Should().BeOfType<CheckPowTransactionRule>();
            partialValidationRules[7].Should().BeOfType<CheckSigOpsRule>();

            List<IFullValidationConsensusRule> fullValidationRules = network.Consensus.FullValidationRules;

            fullValidationRules.Count.Should().Be(5);

            fullValidationRules[0].Should().BeOfType<SetActivationDeploymentsFullValidationRule>();
            fullValidationRules[1].Should().BeOfType<LoadCoinviewRule>();
            fullValidationRules[2].Should().BeOfType<TransactionDuplicationActivationRule>();
            fullValidationRules[3].Should().BeOfType<PowCoinviewRule>();
            fullValidationRules[4].Should().BeOfType<SaveCoinviewRule>();
        }

        [Fact]
        public void StratisConsensusRulesRegistrationTest()
        {
            Network network = new StratisTest();
            new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().RegisterRules(network.Consensus);

            List<IHeaderValidationConsensusRule> headerValidationRules = network.Consensus.HeaderValidationRules;

            headerValidationRules.Count.Should().Be(7);
            headerValidationRules[0].Should().BeOfType<HeaderTimeChecksRule>();
            headerValidationRules[1].Should().BeOfType<HeaderTimeChecksPosRule>();
            headerValidationRules[2].Should().BeOfType<StratisBugFixPosFutureDriftRule>();
            headerValidationRules[3].Should().BeOfType<CheckDifficultyPosRule>();
            headerValidationRules[4].Should().BeOfType<StratisHeaderVersionRule>();
            headerValidationRules[5].Should().BeOfType<ProvenHeaderSizeRule>();
            headerValidationRules[6].Should().BeOfType<ProvenHeaderCoinstakeRule>();

            List<IIntegrityValidationConsensusRule> integrityValidationRules = network.Consensus.IntegrityValidationRules;

            integrityValidationRules.Count.Should().Be(3);
            integrityValidationRules[0].Should().BeOfType<BlockMerkleRootRule>();
            integrityValidationRules[1].Should().BeOfType<PosBlockSignatureRepresentationRule>();
            integrityValidationRules[2].Should().BeOfType<PosBlockSignatureRule>();

            List<IPartialValidationConsensusRule> partialValidationRules = network.Consensus.PartialValidationRules;

            partialValidationRules.Count.Should().Be(11);

            partialValidationRules[0].Should().BeOfType<SetActivationDeploymentsPartialValidationRule>();
            partialValidationRules[1].Should().BeOfType<PosTimeMaskRule>();
            partialValidationRules[2].Should().BeOfType<TransactionLocktimeActivationRule>();
            partialValidationRules[3].Should().BeOfType<CoinbaseHeightActivationRule>();
            partialValidationRules[4].Should().BeOfType<WitnessCommitmentsRule>();
            partialValidationRules[5].Should().BeOfType<BlockSizeRule>();
            partialValidationRules[6].Should().BeOfType<EnsureCoinbaseRule>();
            partialValidationRules[7].Should().BeOfType<CheckPowTransactionRule>();
            partialValidationRules[8].Should().BeOfType<CheckPosTransactionRule>();
            partialValidationRules[9].Should().BeOfType<CheckSigOpsRule>();
            partialValidationRules[10].Should().BeOfType<PosCoinstakeRule>();

            List<IFullValidationConsensusRule> fullValidationRules = network.Consensus.FullValidationRules;

            fullValidationRules.Count.Should().Be(7);

            fullValidationRules[0].Should().BeOfType<SetActivationDeploymentsFullValidationRule>();
            fullValidationRules[1].Should().BeOfType<CheckDifficultyHybridRule>();
            fullValidationRules[2].Should().BeOfType<LoadCoinviewRule>();
            fullValidationRules[3].Should().BeOfType<TransactionDuplicationActivationRule>();
            fullValidationRules[4].Should().BeOfType<PosCoinviewRule>();
            fullValidationRules[5].Should().BeOfType<PosColdStakingRule>();
            fullValidationRules[6].Should().BeOfType<SaveCoinviewRule>();
        }
    }
}
