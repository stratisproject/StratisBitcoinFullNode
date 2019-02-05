﻿using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.PoW.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts.PoW
{
    public sealed class SmartContractPowRuleRegistration : IRuleRegistration
    {
        private readonly Network network;

        public SmartContractPowRuleRegistration(Network network)
        {
            this.network = network;
        }

        public RuleContainer CreateRules()
        {
            var headerValidationRules = new List<HeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksRule(),
                new CheckDifficultyPowRule(),
                new BitcoinActivationRule(this.network.Consensus),
                new BitcoinHeaderVersionRule()
            };

            var integrityValidationRules = new List<IntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule()
            };

            var partialValidationRules = new List<PartialValidationConsensusRule>()
            {
                new SetActivationDeploymentsPartialValidationRule(),

                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new WitnessCommitmentsRule(), // BIP141, BIP144
                new BlockSizeRule(),

                // rules that are inside the method CheckBlock
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckSigOpsRule(),
                new AllowedScriptTypeRule()
            };

            var fullValidationRules = new List<FullValidationConsensusRule>()
            {
                new SetActivationDeploymentsFullValidationRule(),

                new LoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new CanGetSenderRule(new SenderRetriever()),
                new SmartContractFormatRule(new CallDataSerializer(new ContractPrimitiveSerializer(this.network))), // Can we inject these serializers?
                new P2PKHNotContractRule(),
                new SmartContractPowCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward 
                new SaveCoinviewRule()
            };

            return new RuleContainer(fullValidationRules, partialValidationRules, headerValidationRules, integrityValidationRules);
        }
    }
}