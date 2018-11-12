using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.ProvenHeaders.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.ProvenHeaders
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuildeProvenHeadersExtension
    {
        public static IFullNodeBuilder UseProvenHeaders(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ProvenHeadersFeature>("provenheaders");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ProvenHeadersFeature>()
                    .DependOn<PosConsensusFeature>()
                    .FeatureServices(services =>
                    {
                        services
                            .AddSingleton<ProvenHeadersConsensusManagerBehavior>()
                            .AddSingleton<ProvenHeadersConnectionManagerBehavior>();

                        new ProvenHeadersRulesRegistration().RegisterRules(fullNodeBuilder.Network.Consensus);
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Class used to register Proven Headers Rules.
        /// Requires a PoS <see cref="IConsensus"/>
        /// </summary>
        /// <seealso cref="Stratis.Bitcoin.Consensus.Rules.IRuleRegistration" />
        public class ProvenHeadersRulesRegistration : IRuleRegistration
        {
            public void RegisterRules(IConsensus consensus)
            {
                if (!consensus.IsProofOfStake)
                {
                    throw new Exception("Expected PoS consensus (IsProofOfStake is false)");
                }

                Guard.Assert(consensus.HeaderValidationRules.Count > 0); //ensure I've already some rules

                // append Proven Headers rules to current PoS rules
                consensus.HeaderValidationRules.Add(new ProvenHeaderSizeRule());
                consensus.HeaderValidationRules.Add(new ProvenHeaderCoinstakeRule());
            }
        }
    }
}