using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractPosRuleRegistration : IRuleRegistration
    {
        public void RegisterRules(IConsensus consensus)
        {
            consensus.HeaderValidationRules = new List<IHeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksRule(),
                new HeaderTimeChecksPosRule(),
                new StratisBigFixPosFutureDriftRule(),
                new CheckDifficultyPosRule(),
                new StratisHeaderVersionRule(),
            };

            consensus.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule(),
                new SmartContractPosBlockSignatureRule(),
            };

            consensus.PartialValidationRules = new List<IPartialValidationConsensusRule>()
            {
                new SetActivationDeploymentsPartialValidationRule(),

                new CheckDifficultyHybridRule(),
                new PosTimeMaskRule(),

                // rules that are inside the method ContextualCheckBlock
                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new WitnessCommitmentsRule(), // BIP141, BIP144
                new BlockSizeRule(),

                new PosBlockContextRule(), // TODO: this rule needs to be implemented

                // rules that are inside the method CheckBlock
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckPosTransactionRule(),
                new CheckSigOpsRule(),
                new PosCoinstakeRule(),
                new P2PKHNotContractRule()
            };

            consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
            {
                new SetActivationDeploymentsFullValidationRule(),

                new SmartContractLoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new SmartContractPosCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward calculation
                new SmartContractSaveCoinviewRule()
            };
        }
    }
}