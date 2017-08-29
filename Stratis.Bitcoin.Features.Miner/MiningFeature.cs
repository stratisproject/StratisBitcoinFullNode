using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.Features.Miner
{
    public class MiningFeature : FullNodeFeature
    {
        public MiningFeature()
        {
        }

        ///<inheritdoc />
        public override void Start()
        {
        }

        ///<inheritdoc />
        public override void Stop()
        {
        }

        ///<inheritdoc />
        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {            
            if (services.ServiceProvider.GetService<PosMinting>() != null)
            {
                services.Features.EnsureFeature<WalletFeature>();
            }
        }
    }

    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static partial class IFullNodeBuilderExtensions
    {
        public static IFullNodeBuilder AddMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<PowMining>();
                        services.AddSingleton<AssemblerFactory, PowAssemblerFactory>();
                        services.AddSingleton<MiningRPCController>();
                    });
            });
            
            return fullNodeBuilder;
        }

        public static IFullNodeBuilder AddPowPosMining(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<MiningFeature>("mining");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<MiningFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<PowMining>();
                        services.AddSingleton<PosMinting>();
                        services.AddSingleton<AssemblerFactory, PosAssemblerFactory>();
                        services.AddSingleton<MiningRPCController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}
