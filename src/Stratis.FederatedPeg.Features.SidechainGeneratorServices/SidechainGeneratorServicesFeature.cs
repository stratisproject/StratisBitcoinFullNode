using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Miner;

namespace Stratis.FederatedPeg.Features.SidechainGeneratorServices
{
    /// <summary>
    /// This feature provides two services required on the sidechain for the Sidechain Generator.
    /// It outputs the multi-sig redeem script and address into the federation folder and it mines
    /// the first blocks while directing the mining reward into the multi-sig.  This step provides
    /// locked up funds that are used to fund deposit transactions.
    /// </summary>
    public class SidechainGeneratorServicesFeature : FullNodeFeature
    {
        public override void Initialize()
        {
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainGeneratorServicesExtension
    {
        public static IFullNodeBuilder AddSidechainGeneratorServices(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SidechainGeneratorServicesFeature>()
                    .DependOn<MiningFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<SidechainGeneratorServicesController>();
                        services.AddSingleton<ISidechainGeneratorServicesManager, SidechainGeneratorServicesManager>();
                    });
            });
            return fullNodeBuilder;
        }
    }
}
