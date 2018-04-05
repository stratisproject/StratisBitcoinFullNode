using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;

namespace Stratis.FederatedPeg.Features.MainchainGeneratorServices
{
    /// <summary>
    /// This feature provides services required to initialize the sidechain used by the Sidechain Generator.
    /// It outputs the multi-sig redeem scripts and addresses into the federation folder and it mines
    /// the first blocks while directing the mining reward into the multi-sig.  This step provides
    /// locked up funds that are used to fund deposit transactions.
    /// The key and address for mainchain is generated here.  This class then acts as an API client and
    /// calls the sidechain to generate its key and address before activating the premine. 
    /// </summary>
    public class MainchainGeneratorServicesFeature : FullNodeFeature
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
        public static IFullNodeBuilder AddMainchainGeneratorServices(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MainchainGeneratorServicesFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<MainchainGeneratorServicesController>();
                        services.AddSingleton<IMainchainGeneratorServicesManager, MainchainGeneratorServicesManager>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
