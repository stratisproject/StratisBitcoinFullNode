using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.PoS.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.PoS
{
    public sealed class SmartContractPosRuleRegistration : IRuleRegistration
    {
        public RuleContainer CreateRules()
        {
            var headerValidationRules = new List<HeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksRule(),
                new HeaderTimeChecksPosRule(),
                new StratisBugFixPosFutureDriftRule(),
                new CheckDifficultyPosRule(),
                new StratisHeaderVersionRule(),
            };

            var integrityValidationRules = new List<IntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule(),
                new PosBlockSignatureRepresentationRule(),
                new SmartContractPosBlockSignatureRule(),
            };

            var partialValidationRules = new List<PartialValidationConsensusRule>()
            {
                new SetActivationDeploymentsPartialValidationRule(),

                new PosTimeMaskRule(),

                // rules that are inside the method ContextualCheckBlock
                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new WitnessCommitmentsRule(), // BIP141, BIP144
                new BlockSizeRule(),

                // rules that are inside the method CheckBlock
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckPosTransactionRule(),
                new CheckSigOpsRule(),
                new PosCoinstakeRule()
            };

            // TODO: When looking to make PoS work again, will need to add several of the smart contract consensus rules below (see PoA and PoW implementations)
            var fullValidationRules = new List<FullValidationConsensusRule>()
            {
                new SetActivationDeploymentsFullValidationRule(),

                new CheckDifficultyHybridRule(),
                new LoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new SmartContractPosCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward calculation
                new SaveCoinviewRule()
            };

            return new RuleContainer(fullValidationRules, partialValidationRules, headerValidationRules, integrityValidationRules);
        }
    }
}