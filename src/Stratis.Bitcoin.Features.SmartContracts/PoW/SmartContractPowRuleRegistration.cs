using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.PoW.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoW
{
    public sealed class SmartContractPowRuleRegistration : IRuleRegistration
    {
        private readonly Network network;

        public SmartContractPowRuleRegistration(Network network)
        {
            this.network = network;
        }

        public void RegisterRules(IConsensus consensus)
        {
            consensus.HeaderValidationRules = new List<IHeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksRule(),
                new CheckDifficultyPowRule(),
                new BitcoinActivationRule(),
                new BitcoinHeaderVersionRule()
            };

            consensus.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule()
            };

            consensus.PartialValidationRules = new List<IPartialValidationConsensusRule>()
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
                new AllowedScriptTypeRule(),
                new ContractTransactionValidationRule(new CallDataSerializer(new ContractPrimitiveSerializer(this.network)), new List<IContractTransactionValidationLogic>
                {
                    new SmartContractFormatLogic()
                })
            };

            consensus.FullValidationRules = new List<IFullValidationConsensusRule>()
            {
                new SetActivationDeploymentsFullValidationRule(),

                new LoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new CanGetSenderRule(new SenderRetriever()),
                new P2PKHNotContractRule(),
                new SmartContractPowCoinviewRule(), // implements BIP68, MaxSigOps and BlockReward 
                new SaveCoinviewRule()
            };
        }
    }
}