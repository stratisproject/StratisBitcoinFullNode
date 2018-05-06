using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Stratis.FederatedPeg.Features.SidechainRuntime
{
    public class SidechainRuntimeFeature : FullNodeFeature
    {
        public override void Initialize()
        {
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeBuilderSidechainRuntimeFeatureExtension
    {
        public static IFullNodeBuilder AddSidechainRuntime(this IFullNodeBuilder fullNodeBuilder)
        {
            var descriptor = new ServiceDescriptor(typeof(IWalletTransactionHandler), typeof(FedPegWalletTransactionHandler), ServiceLifetime.Singleton);

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SidechainRuntimeFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.Replace(descriptor);
                        services.AddSingleton<SidechainRuntimeController>();
                        services.AddSingleton<ISidechainRuntimeManager, SidechainRuntimeManager>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
