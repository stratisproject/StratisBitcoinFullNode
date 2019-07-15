using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Features.FederatedPeg.Collateral
{
    public class SmartContractCollateralPoARuleRegistration : SmartContractPoARuleRegistration
    {
        private readonly IInitialBlockDownloadState ibdState;

        private readonly ISlotsManager slotsManager;

        private readonly ICollateralChecker collateralChecker;

        private readonly IDateTimeProvider dateTime;

        public SmartContractCollateralPoARuleRegistration()
        : base()
        {
        }

        public override void RegisterRules(IServiceCollection services)
        {
            base.RegisterRules(services);

            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(CheckCollateralFullValidationRule));

            // SaveCoinviewRule must be the last rule executed because actually it calls CachedCoinView.SaveChanges that causes internal CachedCoinView to be updated
            // see https://dev.azure.com/Stratisplatformuk/StratisBitcoinFullNode/_workitems/edit/3770
            services.Remove(new ServiceDescriptor(typeof(IFullValidationConsensusRule), typeof(SaveCoinviewRule), ServiceLifetime.Singleton));
            services.AddSingleton(typeof(IFullValidationConsensusRule), typeof(SaveCoinviewRule));
        }
    }
}
