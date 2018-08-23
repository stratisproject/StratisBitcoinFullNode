﻿using System.Collections.Generic;
using FluentAssertions;
using NBitcoin.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Xunit;

namespace Stratis.Bitcoin.Features.Consensus.Tests.Rules.CommonRules
{
    public class ConsensusRulesRegistrationTest : TestConsensusRulesUnitTestBase
    {
        [Fact]
        public void StratisMainPowConsensusRulesRegistrationTest()
        {
            new FullNodeBuilderConsensusExtension.PowConsensusRulesRegistration().RegisterRules(this.network.Consensus);

            List<IHeaderValidationConsensusRule> headerValidationRules = this.network.Consensus.HeaderValidationRules;

            headerValidationRules.Count.Should().Be(4);

            headerValidationRules[0].Should().BeOfType<HeaderTimeChecksRule>();
            headerValidationRules[1].Should().BeOfType<CheckDifficultyPowRule>();
            headerValidationRules[2].Should().BeOfType<BitcoinActivationRule>();
            headerValidationRules[3].Should().BeOfType<BitcoinHeaderVersionRule>();

            List<IIntegrityValidationConsensusRule> integrityValidationRules = this.network.Consensus.IntegrityValidationRules;

            integrityValidationRules.Count.Should().Be(1);
            integrityValidationRules[0].Should().BeOfType<BlockMerkleRootRule>();

            List<IPartialValidationConsensusRule> partialValidationRules = this.network.Consensus.PartialValidationRules;

            partialValidationRules.Count.Should().Be(8);

            partialValidationRules[0].Should().BeOfType<SetActivationDeploymentsPartialValidationRule>();
            partialValidationRules[1].Should().BeOfType<TransactionLocktimeActivationRule>();
            partialValidationRules[2].Should().BeOfType<CoinbaseHeightActivationRule>();
            partialValidationRules[3].Should().BeOfType<WitnessCommitmentsRule>();
            partialValidationRules[4].Should().BeOfType<BlockSizeRule>();
            partialValidationRules[5].Should().BeOfType<EnsureCoinbaseRule>();
            partialValidationRules[6].Should().BeOfType<CheckPowTransactionRule>();
            partialValidationRules[7].Should().BeOfType<CheckSigOpsRule>();
                
            List<IFullValidationConsensusRule> fullValidationRules = this.network.Consensus.FullValidationRules;

            fullValidationRules.Count.Should().Be(5);

            fullValidationRules[0].Should().BeOfType<SetActivationDeploymentsFullValidationRule>();
            fullValidationRules[1].Should().BeOfType<LoadCoinviewRule>();
            fullValidationRules[2].Should().BeOfType<TransactionDuplicationActivationRule>();
            fullValidationRules[3].Should().BeOfType<PowCoinviewRule>();
            fullValidationRules[4].Should().BeOfType<SaveCoinviewRule>();
        }

        [Fact]
        public void StratisMainPosConsensusRulesRegistrationTest()
        {
            new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().RegisterRules(this.network.Consensus);

            List<IHeaderValidationConsensusRule> headerValidationRules = this.network.Consensus.HeaderValidationRules;

            headerValidationRules.Count.Should().Be(5);
            headerValidationRules[0].Should().BeOfType<HeaderTimeChecksRule>();
            headerValidationRules[1].Should().BeOfType<HeaderTimeChecksPosRule>();
            headerValidationRules[2].Should().BeOfType<StratisBigFixPosFutureDriftRule>();
            headerValidationRules[3].Should().BeOfType<CheckDifficultyPosRule>();
            headerValidationRules[4].Should().BeOfType<StratisHeaderVersionRule>();

            List<IIntegrityValidationConsensusRule> integrityValidationRules = this.network.Consensus.IntegrityValidationRules;

            integrityValidationRules.Count.Should().Be(2);
            integrityValidationRules[0].Should().BeOfType<BlockMerkleRootRule>();
            integrityValidationRules[1].Should().BeOfType<PosBlockSignatureRule>();

            List <IPartialValidationConsensusRule> partialValidationRules = this.network.Consensus.PartialValidationRules;

            partialValidationRules.Count.Should().Be(13);

            partialValidationRules[0].Should().BeOfType<SetActivationDeploymentsPartialValidationRule>();
            partialValidationRules[1].Should().BeOfType<CheckDifficultykHybridRule>();
            partialValidationRules[2].Should().BeOfType<PosTimeMaskRule>();
            partialValidationRules[3].Should().BeOfType<TransactionLocktimeActivationRule>();
            partialValidationRules[4].Should().BeOfType<CoinbaseHeightActivationRule>();
            partialValidationRules[5].Should().BeOfType<WitnessCommitmentsRule>();
            partialValidationRules[6].Should().BeOfType<BlockSizeRule>();
            partialValidationRules[7].Should().BeOfType<PosBlockContextRule>();
            partialValidationRules[8].Should().BeOfType<EnsureCoinbaseRule>();
            partialValidationRules[9].Should().BeOfType<CheckPowTransactionRule>();
            partialValidationRules[10].Should().BeOfType<CheckPosTransactionRule>();
            partialValidationRules[11].Should().BeOfType<CheckSigOpsRule>();
            partialValidationRules[12].Should().BeOfType<PosCoinstakeRule>();

            List<IFullValidationConsensusRule> fullValidationRules = this.network.Consensus.FullValidationRules;

            fullValidationRules.Count.Should().Be(5);

            fullValidationRules[0].Should().BeOfType<SetActivationDeploymentsFullValidationRule>();
            fullValidationRules[1].Should().BeOfType<LoadCoinviewRule>();
            fullValidationRules[2].Should().BeOfType<TransactionDuplicationActivationRule>();
            fullValidationRules[3].Should().BeOfType<PosCoinviewRule>();
            fullValidationRules[4].Should().BeOfType<SaveCoinviewRule>();
        }
    }
}