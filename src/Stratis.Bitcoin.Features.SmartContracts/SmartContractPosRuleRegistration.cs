using System.Collections.Generic;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractPosRuleRegistration : IRuleRegistration
    {
        public ICollection<IConsensusRule> GetRules()
        {
            return new List<IConsensusRule>
                {
                    // == Header ==
                    new HeaderTimeChecksRule(),
                    new HeaderTimeChecksPosRule(),
                    new StratisBigFixPosFutureDriftRule(),
                    new CheckDifficultyPosRule(),
                    new StratisHeaderVersionRule(),

                    // == Integrity ==
                    new BlockMerkleRootRule(),
                    new SmartContractPosBlockSignatureRule(),

                    // == Partial and Full ==
                    new SetActivationDeploymentsRule(),

                    // == Partial ==
                    new CheckDifficultykHybridRule(),
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

                    // rules that require the store to be loaded (coinview)
                    new SmartContractLoadCoinviewRule(),
                    new TransactionDuplicationActivationRule(), // implements BIP30
                    new SmartContractPosCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward calculation
                    new SmartContractSaveCoinviewRule()
                };
        }
    }
}