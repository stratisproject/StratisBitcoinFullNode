using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public class SmartContractPoARuleRegistration : IRuleRegistration
    {
        private readonly Network network;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
        private readonly PoAConsensusRulesRegistration baseRuleRegistration;
        private readonly IEnumerable<IContractTransactionValidationLogic> validationRules;

        public SmartContractPoARuleRegistration(Network network,
            IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever,
            IReceiptRepository receiptRepository,
            ICoinView coinView,
            IEnumerable<IContractTransactionValidationLogic> validationRules)
        {
            this.baseRuleRegistration = new PoAConsensusRulesRegistration();
            this.network = network;
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
            this.validationRules = validationRules;
        }

        public void RegisterRules(IConsensus consensus)
        {
            this.baseRuleRegistration.RegisterRules(consensus);

            // Add SC-Specific partial rules
            var txValidationRules = new List<IContractTransactionValidationLogic>(this.validationRules)
            {
                new SmartContractFormatLogic()                
            };
            consensus.PartialValidationRules.Add(new AllowedScriptTypeRule(this.network));
            consensus.PartialValidationRules.Add(new ContractTransactionValidationRule(this.callDataSerializer, txValidationRules));

            int existingCoinViewRule = consensus.FullValidationRules
                .FindIndex(c => c is CoinViewRule);

            // Replace coinview rule
            consensus.FullValidationRules[existingCoinViewRule] =
                new SmartContractPoACoinviewRule(this.stateRepositoryRoot, this.executorFactory,
                    this.callDataSerializer, this.senderRetriever, this.receiptRepository, this.coinView);

            // Add SC-specific full rules BEFORE the coinviewrule
            var scRules = new List<IFullValidationConsensusRule>
            {
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new CanGetSenderRule(this.senderRetriever),
                new P2PKHNotContractRule(this.stateRepositoryRoot)
            };

            consensus.FullValidationRules.InsertRange(existingCoinViewRule, scRules);
        }
    }
}
