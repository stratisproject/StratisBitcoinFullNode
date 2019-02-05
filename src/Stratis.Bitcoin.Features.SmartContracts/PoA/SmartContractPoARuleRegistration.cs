using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.Core.Util;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.CLR.Serialization;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public class SmartContractPoARuleRegistration : IRuleRegistration
    {
        private readonly Network network;

        public SmartContractPoARuleRegistration(Network network)
        {
            this.network = network;
        }

        public RuleContainer CreateRules()
        {
            var headerValidationRules = new List<HeaderValidationConsensusRule>()
            {
                new HeaderTimeChecksPoARule(),
                new StratisHeaderVersionRule(),
                new PoAHeaderDifficultyRule(),
                new PoAHeaderSignatureRule()
            };

            var integrityValidationRules = new List<IntegrityValidationConsensusRule>()
            {
                new BlockMerkleRootRule(),
                new PoAIntegritySignatureRule()
            };

            var partialValidationRules = new List<PartialValidationConsensusRule>()
            {
                new SetActivationDeploymentsPartialValidationRule(),

                // rules that are inside the method ContextualCheckBlock
                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
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

                // rules that require the store to be loaded (coinview)
                new LoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new CanGetSenderRule(new SenderRetriever()),
                new SmartContractFormatRule(new CallDataSerializer(new ContractPrimitiveSerializer(this.network))), // Can we inject these serializers?
                new P2PKHNotContractRule(),
                new SmartContractPoACoinviewRule(),
                new SaveCoinviewRule()
            };

            return new RuleContainer(fullValidationRules, partialValidationRules, headerValidationRules, integrityValidationRules);
        }
    }
}
