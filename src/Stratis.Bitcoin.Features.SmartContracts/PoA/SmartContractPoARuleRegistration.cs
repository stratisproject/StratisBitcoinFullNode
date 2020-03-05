using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
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
    /// <inheritdoc />
    public class SmartContractPoARuleRegistration : IRuleRegistration
    {
        /// <inheritdoc />
        public virtual void RegisterRules(IServiceCollection services)
        {
            // TODO: Do what the rest of the FN code did and move the rule registration to the network class.
            new PoAConsensusRulesRegistration().RegisterRules(services);

            if (!services.Any(s => s.ImplementationType == typeof(SmartContractFormatLogic)))
                services.AddSingleton(typeof(IContractTransactionPartialValidationRule), typeof(SmartContractFormatLogic));

            if (!services.Any(s => s.ImplementationType == typeof(AllowedScriptTypeRule)))
                services.AddSingleton(typeof(IPartialValidationConsensusRule), typeof(AllowedScriptTypeRule));

            if (!services.Any(s => s.ImplementationType == typeof(ContractTransactionPartialValidationRule)))
                services.AddSingleton(typeof(IPartialValidationConsensusRule), typeof(ContractTransactionPartialValidationRule));

            if (!services.Any(s => s.ImplementationType == typeof(ContractTransactionFullValidationRule)))
                services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(ContractTransactionFullValidationRule));

            // Add SC-specific full rules BEFORE the coinviewrule
            foreach (Type ruleType in new List<Type>()
            {
                typeof(TxOutSmartContractExecRule),
                typeof(OpSpendRule),
                typeof(CanGetSenderRule),
                typeof(P2PKHNotContractRule)
            })
            {
                if (!services.Any(s => s.ImplementationType == ruleType))
                    services.AddSingleton(typeof(IFullValidationConsensusRule), ruleType);
            }

            // Remove the base POA coinview rule.
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(PoACoinviewRule)).ToList())
            {
                services.Remove(serviceDescriptor);
            }

            // Remove the SmartContractPoACoinviewRule as it is position wrong in the order of execution.
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(SmartContractPoACoinviewRule)).ToList())
            {
                services.Remove(serviceDescriptor);
            }

            // Re-add it to the "back" of the rules list.
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SmartContractPoACoinviewRule));

            AddSaveCoinViewAsLastRule(services);
        }

        /// <summary>
        /// Adds the <see cref="SaveCoinviewRule"/> to the back of the rules collection.
        /// <para>
        /// SaveCoinviewRule must be the last rule executed because actually it calls CachedCoinView.SaveChanges that causes internal CachedCoinView to be updated
        /// see https://dev.azure.com/Stratisplatformuk/StratisBitcoinFullNode/_workitems/edit/3770.
        /// </para>
        /// </summary>
        public void AddSaveCoinViewAsLastRule(IServiceCollection services)
        {
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(SaveCoinviewRule)).ToList())
            {
                services.Remove(serviceDescriptor);
            }

            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SaveCoinviewRule));
        }
    }
}
