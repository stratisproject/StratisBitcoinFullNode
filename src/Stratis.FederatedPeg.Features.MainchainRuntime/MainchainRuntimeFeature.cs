using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

//This is experimental while we are waiting for a generic OP_RETURN function in the full node wallet.

namespace Stratis.FederatedPeg.Features.MainchainRuntime
{
    public class MainchainRuntimeFeature : FullNodeFeature
    {
        public override void Initialize()
        {
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderMainchainRuntimeFeatureExtension
    {
        public static IFullNodeBuilder AddMainchainRuntime(this IFullNodeBuilder fullNodeBuilder)
        {
            var descriptor = new ServiceDescriptor(typeof(IWalletTransactionHandler), typeof(FedPegWalletTransactionHandler), ServiceLifetime.Singleton);
            
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MainchainRuntimeFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.Replace(descriptor);
                        services.AddSingleton<MainchainRuntimeController>();
                        services.AddSingleton<IMainchainRuntimeManager, MainchainRuntimeManager>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
