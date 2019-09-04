using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.PoA.BasePoAFeatureConsensusRules;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.PoA
{
    public class SmartContractPoARuleRegistration : IRuleRegistration
    {
        protected readonly Network network;

        public SmartContractPoARuleRegistration()
        {
        }

        public virtual void RegisterRules(IServiceCollection services)
        {
            // TODO: Do what the rest of the FN code did and move the rule registration to the network class.
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
