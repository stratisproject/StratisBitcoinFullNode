using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Rules;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.LightD
{
    public static class FullNodeBuilderExtensions
    {
        public static IFullNodeBuilder UseLightPowConsensus(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<PowConsensusFeature>("lightpowconsensus");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PowConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<ConsensusOptions, ConsensusOptions>();
                        services.AddSingleton<DBreezeCoinView>();
                        services.AddSingleton<ICoinView, CachedCoinView>();
                        services.AddSingleton<ConsensusController>();
                        services.AddSingleton<IConsensusRuleEngine, PowConsensusRuleEngine>();
                        services.AddSingleton<IChainState, ChainState>();
                        services.AddSingleton<ConsensusQuery>()
                            .AddSingleton<INetworkDifficulty, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>())
                            .AddSingleton<IGetUnspentTransaction, ConsensusQuery>(provider => provider.GetService<ConsensusQuery>());
                        new LightPowConsensusRulesRegistration().RegisterRules(fullNodeBuilder.Network.Consensus);
                    });
            });

            return fullNodeBuilder;
        }

        public class LightPowConsensusRulesRegistration : IRuleRegistration
        {
            public void RegisterRules(IConsensus consensus)
            {
                consensus.HeaderValidationRules = new List<IHeaderValidationConsensusRule>();

                consensus.IntegrityValidationRules = new List<IIntegrityValidationConsensusRule>();

                consensus.PartialValidationRules = new List<IPartialValidationConsensusRule>();

                consensus.FullValidationRules = new List<IFullValidationConsensusRule>();
            }
        }
    }
}