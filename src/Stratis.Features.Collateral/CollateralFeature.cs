using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.PoA;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Features.Collateral.CounterChain;

namespace Stratis.Features.Collateral
{
    /// <summary>
    /// Sets up the necessary components to check the collateral requirement is met on the counter chain.
    /// </summary>
    public class CollateralFeature : FullNodeFeature
    {
        private readonly ICollateralChecker collateralChecker;

        public CollateralFeature(ICollateralChecker collateralChecker)
        {
            this.collateralChecker = collateralChecker;
        }

        public override async Task InitializeAsync()
        {
            await this.collateralChecker.InitializeAsync().ConfigureAwait(false);
        }

        public override void Dispose()
        {
            this.collateralChecker?.Dispose();
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderCollateralFeatureExtension
    {
        public static IFullNodeBuilder CheckForPoAMembersCollateral(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features.AddFeature<CollateralFeature>()
                    .DependOn<CounterChainFeature>()
                    .DependOn<PoAFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IFederationManager, CollateralFederationManager>();
                        services.AddSingleton<ICollateralChecker, CollateralChecker>();

                        new SmartContractCollateralPoARuleRegistration().RegisterRules(services);
                    });
            });

            return fullNodeBuilder;
        }

        /// <summary>
        /// Adds mining to the smart contract node when on a proof-of-authority network with collateral enabled.
        /// </summary>
        public static IFullNodeBuilder UseSmartContractCollateralPoAMining(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<PoAFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IFederationManager, FederationManager>();
                        services.AddSingleton<PoABlockHeaderValidator>();
                        services.AddSingleton<IPoAMiner, CollateralPoAMiner>();
                        services.AddSingleton<PoAMinerSettings>();
                        services.AddSingleton<MinerSettings>();
                        services.AddSingleton<ISlotsManager, SlotsManager>();
                        services.AddSingleton<BlockDefinition, SmartContractPoABlockDefinition>();
                        services.AddSingleton<IBlockBufferGenerator, BlockBufferGenerator>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
