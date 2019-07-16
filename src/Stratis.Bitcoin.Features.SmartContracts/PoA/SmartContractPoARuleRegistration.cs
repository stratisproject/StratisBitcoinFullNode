using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
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
        protected readonly Network network;
        private readonly IStateRepositoryRoot stateRepositoryRoot;
        private readonly IContractExecutorFactory executorFactory;
        private readonly ICallDataSerializer callDataSerializer;
        private readonly ISenderRetriever senderRetriever;
        private readonly IReceiptRepository receiptRepository;
        private readonly ICoinView coinView;
       // private readonly PoAConsensusRulesRegistration baseRuleRegistration;
        private readonly IEnumerable<IContractTransactionPartialValidationRule> partialTxValidationRules;
        private readonly IEnumerable<IContractTransactionFullValidationRule> fullTxValidationRules;

        public SmartContractPoARuleRegistration()
        {
        }

        public virtual void RegisterRules(IServiceCollection services)
        {
            // TODO: this is not needed anymore as the default rules are registered in network
            new PoAConsensusRulesRegistration().RegisterRules(services);

            // Add SC-Specific partial rules
            //var txValidationRules = new List<IContractTransactionPartialValidationRule>(this.partialTxValidationRules)
            //{
            //    new SmartContractFormatLogic()
            //};
            services.AddSingleton(typeof(IContractTransactionPartialValidationRule), typeof(SmartContractFormatLogic));

            services.AddSingleton(typeof(IPartialValidationConsensusRule), typeof(AllowedScriptTypeRule));
            //consensus.ConsensusRules.PartialValidationRules.Add(typeof(AllowedScriptTypeRule));

            services.AddSingleton(typeof(IPartialValidationConsensusRule), typeof(ContractTransactionPartialValidationRule));
            //consensus.ConsensusRules.PartialValidationRules.Add((Type) new ContractTransactionPartialValidationRule(this.callDataSerializer, txValidationRules));

            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(ContractTransactionFullValidationRule));

            // Add SC-specific full rules BEFORE the coinviewrule
            foreach (Type ruleType in new List<Type>()
            {
                typeof(TxOutSmartContractExecRule),
                typeof(OpSpendRule),
                typeof(CanGetSenderRule),
                typeof(P2PKHNotContractRule)
            })
                services.AddSingleton(typeof(IFullValidationConsensusRule), ruleType);

            // Replace coinview rule
            services.Remove(services.Single(f => f.ImplementationType == typeof(PoACoinviewRule)));
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SmartContractPoACoinviewRule));

            // SaveCoinviewRule must be the last rule executed because actually it calls CachedCoinView.SaveChanges that causes internal CachedCoinView to be updated
            // see https://dev.azure.com/Stratisplatformuk/StratisBitcoinFullNode/_workitems/edit/3770
            services.Remove(services.Single(f => f.ImplementationType == typeof(SaveCoinviewRule)));
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SaveCoinviewRule));
        }
    }
}
