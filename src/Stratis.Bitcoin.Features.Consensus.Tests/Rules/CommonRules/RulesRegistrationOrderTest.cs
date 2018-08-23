using System.Collections.Generic;
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

            Assert.True(headerValidationRules.Count == 4);
            Assert.True(headerValidationRules[0] is HeaderTimeChecksRule);
            Assert.True(headerValidationRules[1] is CheckDifficultyPowRule);
            Assert.True(headerValidationRules[2] is BitcoinActivationRule);
            Assert.True(headerValidationRules[3] is BitcoinHeaderVersionRule);

            List<IIntegrityValidationConsensusRule> integrityValidationRules = this.network.Consensus.IntegrityValidationRules;

            Assert.True(integrityValidationRules.Count == 1);
            Assert.True(integrityValidationRules[0] is BlockMerkleRootRule);

            List<IPartialValidationConsensusRule> partialValidationRules = this.network.Consensus.PartialValidationRules;

            Assert.True(partialValidationRules.Count == 8);

            Assert.True(partialValidationRules[0] is SetActivationDeploymentsPartialValidationRule);
            Assert.True(partialValidationRules[1] is TransactionLocktimeActivationRule);
            Assert.True(partialValidationRules[2] is CoinbaseHeightActivationRule);
            Assert.True(partialValidationRules[3] is WitnessCommitmentsRule);
            Assert.True(partialValidationRules[4] is BlockSizeRule);
            Assert.True(partialValidationRules[5] is EnsureCoinbaseRule);
            Assert.True(partialValidationRules[6] is CheckPowTransactionRule);
            Assert.True(partialValidationRules[7] is CheckSigOpsRule);
                
            List<IFullValidationConsensusRule> fullValidationRules = this.network.Consensus.FullValidationRules;

            Assert.True(fullValidationRules.Count == 5);

            Assert.True(fullValidationRules[0] is SetActivationDeploymentsFullValidationRule);
            Assert.True(fullValidationRules[1] is LoadCoinviewRule);
            Assert.True(fullValidationRules[2] is TransactionDuplicationActivationRule);
            Assert.True(fullValidationRules[3] is PowCoinviewRule);
            Assert.True(fullValidationRules[4] is SaveCoinviewRule);
        }

        [Fact]
        public void StratisMainPosConsensusRulesRegistrationTest()
        {
            new FullNodeBuilderConsensusExtension.PosConsensusRulesRegistration().RegisterRules(this.network.Consensus);

            List<IHeaderValidationConsensusRule> headerValidationRules = this.network.Consensus.HeaderValidationRules;

            Assert.True(headerValidationRules.Count == 5);
            Assert.True(headerValidationRules[0] is HeaderTimeChecksRule);
            Assert.True(headerValidationRules[1] is HeaderTimeChecksPosRule);
            Assert.True(headerValidationRules[2] is StratisBigFixPosFutureDriftRule);
            Assert.True(headerValidationRules[3] is CheckDifficultyPosRule);
            Assert.True(headerValidationRules[4] is StratisHeaderVersionRule);

            List<IIntegrityValidationConsensusRule> integrityValidationRules = this.network.Consensus.IntegrityValidationRules;

            Assert.True(integrityValidationRules.Count == 2);
            Assert.True(integrityValidationRules[0] is BlockMerkleRootRule);
            Assert.True(integrityValidationRules[1] is PosBlockSignatureRule);

            List <IPartialValidationConsensusRule> partialValidationRules = this.network.Consensus.PartialValidationRules;

            Assert.True(partialValidationRules.Count == 13);

            Assert.True(partialValidationRules[0] is SetActivationDeploymentsPartialValidationRule);
            Assert.True(partialValidationRules[1] is CheckDifficultykHybridRule);
            Assert.True(partialValidationRules[2] is PosTimeMaskRule);
            Assert.True(partialValidationRules[3] is TransactionLocktimeActivationRule);
            Assert.True(partialValidationRules[4] is CoinbaseHeightActivationRule);
            Assert.True(partialValidationRules[5] is WitnessCommitmentsRule);
            Assert.True(partialValidationRules[6] is BlockSizeRule);
            Assert.True(partialValidationRules[7] is PosBlockContextRule);
            Assert.True(partialValidationRules[8] is EnsureCoinbaseRule);
            Assert.True(partialValidationRules[9] is CheckPowTransactionRule);
            Assert.True(partialValidationRules[10] is CheckPosTransactionRule);
            Assert.True(partialValidationRules[11] is CheckSigOpsRule);
            Assert.True(partialValidationRules[12] is PosCoinstakeRule);

            List<IFullValidationConsensusRule> fullValidationRules = this.network.Consensus.FullValidationRules;

            Assert.True(fullValidationRules.Count == 5);

            Assert.True(fullValidationRules[0] is SetActivationDeploymentsFullValidationRule);
            Assert.True(fullValidationRules[1] is LoadCoinviewRule);
            Assert.True(fullValidationRules[2] is TransactionDuplicationActivationRule);
            Assert.True(fullValidationRules[3] is PosCoinviewRule);
            Assert.True(fullValidationRules[4] is SaveCoinviewRule);
        }
    }
}