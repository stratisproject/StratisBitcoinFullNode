using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
      //  private readonly PoAConsensusRulesRegistration baseRuleRegistration;
        private readonly IEnumerable<IContractTransactionPartialValidationRule> partialTxValidationRules;
        private readonly IEnumerable<IContractTransactionFullValidationRule> fullTxValidationRules;

        public SmartContractPoARuleRegistration(Network network,
            IStateRepositoryRoot stateRepositoryRoot,
            IContractExecutorFactory executorFactory,
            ICallDataSerializer callDataSerializer,
            ISenderRetriever senderRetriever,
            IReceiptRepository receiptRepository,
            ICoinView coinView,
            IEnumerable<IContractTransactionPartialValidationRule> partialTxValidationRules,
            IEnumerable<IContractTransactionFullValidationRule> fullTxValidationRules)
        {
           // this.baseRuleRegistration = new PoAConsensusRulesRegistration();
            this.network = network;
            this.stateRepositoryRoot = stateRepositoryRoot;
            this.executorFactory = executorFactory;
            this.callDataSerializer = callDataSerializer;
            this.senderRetriever = senderRetriever;
            this.receiptRepository = receiptRepository;
            this.coinView = coinView;
            this.partialTxValidationRules = partialTxValidationRules;
            this.fullTxValidationRules = fullTxValidationRules;
        }

        public void RegisterRules(IConsensus consensus)
        {
            // this.baseRuleRegistration.RegisterRules(consensus); this should already be set

            // Add SC-Specific partial rules
            var txValidationRules = new List<IContractTransactionPartialValidationRule>(this.partialTxValidationRules)
            {
                new SmartContractFormatLogic()                
            };
            consensus.ConsensusRules.PartialValidationRules.Add(typeof(AllowedScriptTypeRule));
            consensus.ConsensusRules.PartialValidationRules.Add(typeof(ContractTransactionPartialValidationRule));

            consensus.ConsensusRules.FullValidationRules.Add(typeof(ContractTransactionFullValidationRule));

            int existingCoinViewRule = consensus.ConsensusRules.FullValidationRules.FindIndex(c => c == typeof(CoinViewRule));

            // Replace coinview rule
            consensus.ConsensusRules.FullValidationRules[existingCoinViewRule] = typeof(SmartContractPoACoinviewRule);

            // Add SC-specific full rules BEFORE the coinviewrule
            var scRules = new List<Type>
            {
                typeof(TxOutSmartContractExecRule),
                typeof(OpSpendRule),
                typeof(CanGetSenderRule),
                typeof(P2PKHNotContractRule)
            };

            consensus.ConsensusRules.FullValidationRules.InsertRange(existingCoinViewRule, scRules);
        }
    }
}
