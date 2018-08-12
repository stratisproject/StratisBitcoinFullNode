using System.Collections.Generic;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractPowRuleRegistration : IRuleRegistration
    {
        public ICollection<IConsensusRule> GetRules()
        {
            var rules = new List<IConsensusRule>
            {
                // == Header ==
                new HeaderTimeChecksRule(),
                new CheckDifficultyPowRule(),
                new BitcoinActivationRule(),
                new BitcoinHeaderVersionRule(),

                // == Integrity ==
                new BlockMerkleRootRule(),

                // == Partial and Full ==
                new SetActivationDeploymentsRule(),

                // == Partial ==
                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new WitnessCommitmentsRule(), // BIP141, BIP144
                new BlockSizeRule(),

                // rules that are inside the method CheckBlock
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckSigOpsRule(),

                // == Full ==                

                // rules that require the store to be loaded (coinview)
                new SmartContractLoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30

                // Smart contract specific rules
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new SmartContractPowCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward 
                new SmartContractSaveCoinviewRule()
            };

            return rules;
        }
    }
}