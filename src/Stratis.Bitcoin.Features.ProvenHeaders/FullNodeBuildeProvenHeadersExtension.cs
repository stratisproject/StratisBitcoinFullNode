using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.ProvenHeaders.Behaviors;
using Stratis.Bitcoin.Features.ProvenHeaders.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.ProvenHeaders
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuildeProvenHeadersExtension
    {
        /// <summary>
        /// Uses the Proven Headers feature.
        /// </summary>
        /// <param name="fullNodeBuilder">The full node builder.</param>
        /// <param name="allowLegacyHeadersForWhitelistedPeers">If set to <c>true</c> allows legacy Headers protocol to be valid for white listed peers.</param>
        /// <returns></returns>
        public static IFullNodeBuilder UseProvenHeaders(this IFullNodeBuilder fullNodeBuilder, bool allowLegacyHeadersForWhitelistedPeers = false)
        {
            LoggingConfiguration.RegisterFeatureNamespace<ProvenHeadersFeature>("provenheaders");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ProvenHeadersFeature>()
                    .DependOn<PosConsensusFeature>()
                    .DependOn<BlockStoreFeature>()
                    .FeatureServices(services =>
                    {
                        services
                            .AddSingleton<ProvenHeadersConsensusManagerBehavior>()
                            .AddSingleton<ProvenHeadersConnectionManagerBehavior>()
                            .AddSingleton<ProvenHeadersBlockStoreBehavior>();

                        if (allowLegacyHeadersForWhitelistedPeers)
                        {
                            services.AddSingleton<WhitelistedLegacyPeerAllowed>();
                        }

                        new ProvenHeadersRulesRegistration().RegisterRules(fullNodeBuilder.Network.Consensus);
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Class used to register Proven Headers Rules.
        /// </summary>
        /// <seealso cref="Stratis.Bitcoin.Consensus.Rules.IRuleRegistration" />
        public class ProvenHeadersRulesRegistration : IRuleRegistration
        {
            public void RegisterRules(IConsensus consensus)
            {
                Guard.Assert(consensus.HeaderValidationRules.Count > 0); //ensure I've already some rules

                // append Proven Headers rules to current PoS rules
                consensus.HeaderValidationRules.Add(new ProvenHeaderSizeRule());
                consensus.HeaderValidationRules.Add(new ProvenHeaderCoinstakeRule());
            }
        }
    }
}