using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using TracerAttributes;

namespace Stratis.Features.Collateral
{
    public class SmartContractCollateralPoARuleRegistration : SmartContractPoARuleRegistration
    {
        public SmartContractCollateralPoARuleRegistration() : base()
        {
        }

        [NoTrace]
        public override void RegisterRules(IServiceCollection services)
        {
            // TODO: Fix this so Cirrus also registers the rules on the network rather than this dirty hack.

            // Both SmartContractPoARuleRegistration and SmartContractCollateralPoARuleRegistration need to register the rules, but only if needed.
            // In the case of CirrusPegD, they won't yet have been registered so registering them here is necessary.
            // In the case of CirrusMinerD, they were already registered so if we register them again we will have 2x each rule and the node will fail
            // on some SingleOrDefaults later on.

            // To check whether scpoa rules are already added, we test whether the specific coinview rule only added by the base exists yet.
            if (services.FirstOrDefault(x => x.ImplementationType == typeof(SmartContractPoACoinviewRule)) == null)
            {
                base.RegisterRules(services);
            }

            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(CheckCollateralFullValidationRule));

            // SaveCoinviewRule must be the last rule executed because actually it calls CachedCoinView.SaveChanges that causes internal CachedCoinView to be updated
            // see https://dev.azure.com/Stratisplatformuk/StratisBitcoinFullNode/_workitems/edit/3770
            foreach (ServiceDescriptor serviceDescriptor in services.Where(f => f.ImplementationType == typeof(SaveCoinviewRule)).ToList()) services.Remove(serviceDescriptor);
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SaveCoinviewRule));
        }
    }
}