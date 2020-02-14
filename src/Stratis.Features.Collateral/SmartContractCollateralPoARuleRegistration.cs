using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.Rules;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA.Rules;
using TracerAttributes;

namespace Stratis.Features.Collateral
{
    public sealed class SmartContractCollateralPoARuleRegistration : SmartContractPoARuleRegistration
    {
        private readonly bool isMiner;

        public SmartContractCollateralPoARuleRegistration(bool isMiner)
        {
            this.isMiner = isMiner;
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

            // On a collateral aware network the commitment height should always be checked.
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(CheckCollateralCommitmentHeightRule));

            // Only if the node is a miner will the full set of collateral checks be done.
            if (this.isMiner)
                services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(CheckCollateralFullValidationRule));

            base.AddSaveCoinViewAsLastRule(services);
        }
    }
}