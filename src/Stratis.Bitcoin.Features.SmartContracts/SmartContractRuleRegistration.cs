﻿using System.Collections.Generic;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractRuleRegistration : IRuleRegistration
    {
        public SmartContractRuleRegistration()
        {
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            var rules = new List<ConsensusRule>
            {
                new TemporarySetChainHeader(),
                
                // == Header ==
                new HeaderTimeChecksRule(),
                new CheckDifficultyPowRule(),
                
                // == Integrity ==
                new BlockMerkleRootRule(),
                
                // == Partial ==
                new SetActivationDeploymentsRule(),

                // rules that are inside the method CheckBlockHeader

                // rules that are inside the method ContextualCheckBlockHeader
                new CheckpointsRule(),
                new AssumeValidRule(),

                // rules that are inside the method ContextualCheckBlock
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
                new SmartContractCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward calculation
                new SmartContractSaveCoinviewRule()
            };

            return rules;
        }
    }
}