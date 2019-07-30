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
        private readonly IEnumerable<IContractTransactionPartialValidationRule> partialTxValidationRules;
        private readonly IEnumerable<IContractTransactionFullValidationRule> fullTxValidationRules;

        public SmartContractPoARuleRegistration()
        {
        }

        public virtual void RegisterRules(IServiceCollection services)
        {
            // TODO: this is not needed anymore as the default rules are registered in network
            new PoAConsensusRulesRegistration().RegisterRules(services);

            services.AddSingleton(typeof(IContractTransactionPartialValidationRule), typeof(SmartContractFormatLogic));

            services.AddSingleton(typeof(IPartialValidationConsensusRule), typeof(AllowedScriptTypeRule));

            services.AddSingleton(typeof(IPartialValidationConsensusRule), typeof(ContractTransactionPartialValidationRule));

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
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(PoACoinviewRule)).ToList()) services.Remove(serviceDescriptor);
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(SmartContractPoACoinviewRule)).ToList()) services.Remove(serviceDescriptor);
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SmartContractPoACoinviewRule));

            // SaveCoinviewRule must be the last rule executed because actually it calls CachedCoinView.SaveChanges that causes internal CachedCoinView to be updated
            // see https://dev.azure.com/Stratisplatformuk/StratisBitcoinFullNode/_workitems/edit/3770
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(SaveCoinviewRule)).ToList()) services.Remove(serviceDescriptor);
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SaveCoinviewRule));
        }
    }
}
